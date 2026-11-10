using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Recycler;

[Singleton]
public interface IRecyclerStore
{
    RecyclerConfiguration Load();
    DateTimeOffset? NextAllowed(int userId);
    RecycledBox? Recycle(int userId, uint boxDefinitionId, RecyclerConfiguration configuration, RecyclerPrize prize,
        IReadOnlyList<RecyclerInput> inputs, DateTimeOffset now, int? currentDuckets = null);
    GiftContent? FindBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId);
    uint? OpenBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId, GiftContent content);
    bool OpenChatStyleBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId, GiftContent content) => false;
}

public sealed class RecyclerStore(IDatabase database, TimeProvider? clock = null) : IRecyclerStore
{
    public RecyclerConfiguration Load()
    {
        using var connection = database.Connection();

        return ReadConfiguration(connection);
    }

    public DateTimeOffset? NextAllowed(int userId)
    {
        using var connection = database.Connection();

        return connection.QuerySingleOrDefault<DateTimeOffset?>("SELECT next_allowed_at FROM user_recycler WHERE user_id=@userId", new { userId });
    }

    public RecycledBox? Recycle(int userId, uint boxDefinitionId, RecyclerConfiguration configuration, RecyclerPrize prize,
        IReadOnlyList<RecyclerInput> inputs, DateTimeOffset now, int? currentDuckets = null)
    {
        if (!configuration.Enabled || configuration.CostDuckets < 0
            || configuration.CostDuckets > 0 && (currentDuckets is null || currentDuckets < configuration.CostDuckets)
            || inputs.Count != configuration.Slots || inputs.Count is < 1 or > 12
            || inputs.Any(input => input.Id == 0) || inputs.Select(input => input.Id).Distinct().Count() != inputs.Count) {
            return null;
        }

        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction) is null
            || !configuration.Matches(ReadConfiguration(connection, transaction))) {
            return null;
        }

        // Create before the locking read: missing-row gap locks must not couple different users.
        connection.Execute("INSERT IGNORE INTO user_recycler(user_id,next_allowed_at) VALUES(@userId,@now)",
            new { userId, now = now.UtcDateTime }, transaction);
        var next = connection.QuerySingleOrDefault<DateTimeOffset?>(
            "SELECT next_allowed_at FROM user_recycler WHERE user_id=@userId FOR UPDATE", new { userId }, transaction);

        if (next > now || !configuration.Levels.SelectMany(level => level.Prizes).Contains(prize)) {
            return null;
        }

        if (connection.ExecuteScalar<uint?>("SELECT id FROM furniture WHERE id=@boxDefinitionId AND BINARY item_name=BINARY @name AND sprite_id=@sprite AND type='s' AND interaction_type IN ('','default') LOCK IN SHARE MODE",
                new { boxDefinitionId, name = RecyclerBox.ClassName, sprite = RecyclerBox.SpriteId }, transaction) is null
            || !ValidPrize(connection, transaction, prize)) {
            return null;
        }

        if (configuration.CostDuckets > 0
            && UserCurrencyStore.Get(connection, userId, ActivityPointType.Duckets, transaction, forUpdate: true) < configuration.CostDuckets) {
            return null;
        }

        var ids = inputs.Select(input => input.Id).ToArray();
        var rows = connection.Query<InputRow>(
            "SELECT id,base_item AS ItemId,limited_number AS LimitedNumber,limited_stack AS LimitedStack FROM items WHERE id IN @ids AND user_id=@userId AND room_id=0 ORDER BY id FOR UPDATE",
            new { ids, userId }, transaction).ToArray();

        if (rows.Length != inputs.Count || rows.Any(row => row.LimitedNumber != 0 || row.LimitedStack != 0)
            || !rows.Select(row => new RecyclerInput(row.Id, row.ItemId)).SequenceEqual(inputs.OrderBy(input => input.Id))) {
            return null;
        }

        var definitionIds = rows.Select(row => row.ItemId).Distinct().ToArray();
        var eligible = connection.Query<uint>("SELECT id FROM furniture WHERE id IN @definitionIds AND allow_recycle=1 LOCK IN SHARE MODE",
            new { definitionIds }, transaction).ToHashSet();

        if (eligible.Count != definitionIds.Length
            || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM room_music_playlist WHERE player_id IN @ids OR disc_id IN @ids", new { ids }, transaction) != 0
            || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items_builders_club WHERE item_id IN @ids", new { ids }, transaction) != 0
            || connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents WHERE item_id IN @ids", new { ids }, transaction) != 0) {
            return null;
        }

        if (connection.Execute("DELETE FROM items WHERE id IN @ids AND user_id=@userId AND room_id=0", new { ids, userId }, transaction) != inputs.Count) {
            throw new DBConcurrencyException("Recycler inputs changed during consumption.");
        }

        var extraData = now.UtcDateTime.ToString("d-M-yyyy", CultureInfo.InvariantCulture);
        connection.Execute("INSERT INTO items(base_item,user_id,room_id,x,y,z,wall_pos,rot,extra_data,limited_number,limited_stack) VALUES(@boxDefinitionId,@userId,0,0,0,0,'',0,@extraData,0,0)",
            new { boxDefinitionId, userId, extraData }, transaction);
        var id = connection.ExecuteScalar<uint>("SELECT LAST_INSERT_ID()", transaction: transaction);
        connection.Execute("INSERT INTO user_presents(item_id,base_id,extra_data,chat_style_id) VALUES(@id,@itemId,'',@styleId)",
            new { id, itemId = prize.ItemId > 0 ? (uint?)prize.ItemId : null, styleId = prize.ChatStyleId > 0 ? (int?)prize.ChatStyleId : null }, transaction);
        int? duckets = null;

        if (configuration.CostDuckets > 0) {
            UserCurrencyStore.Add(connection, userId, ActivityPointType.Duckets, -configuration.CostDuckets, transaction);
            duckets = UserCurrencyStore.Get(connection, userId, ActivityPointType.Duckets, transaction, forUpdate: true);
        }

        connection.Execute("INSERT INTO user_recycler(user_id,next_allowed_at) VALUES(@userId,@next) ON DUPLICATE KEY UPDATE next_allowed_at=@next",
            new { userId, next = now.AddSeconds(configuration.CooldownSeconds).UtcDateTime }, transaction);
        var reference = Guid.NewGuid();
        RecyclerReceiptStore.Recycled(connection, transaction, reference, userId, id, configuration, prize, inputs, duckets, now);

        return RecyclerCommit.Complete(connection, transaction, () => Committed(userId, reference, opening: false))
            ? new(id, extraData, duckets) : null;
    }

    public GiftContent? FindBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId)
    {
        using var connection = database.Connection();
        var rows = connection.Query<ContentRow>("SELECT p.base_id AS BaseId,p.extra_data AS ExtraData,COALESCE(p.chat_style_id,0) AS ChatStyleId FROM user_presents p INNER JOIN items i ON i.id=p.item_id WHERE i.id=@itemId AND i.user_id=@ownerId AND i.room_id=@roomId AND i.base_item=@boxDefinitionId",
            new { itemId, ownerId, roomId, boxDefinitionId }).ToArray();

        return rows.Length == 1 ? new(rows[0].BaseId, rows[0].ExtraData) { ChatStyleId = rows[0].ChatStyleId } : null;
    }

    public uint? OpenBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId, GiftContent content) =>
        content.ChatStyleId == 0 ? Open(itemId, ownerId, roomId, boxDefinitionId, content) : null;

    public bool OpenChatStyleBox(uint itemId, int ownerId, uint roomId, uint boxDefinitionId, GiftContent content) =>
        content.ChatStyleId > 0 && content.BaseId == 0 && Open(itemId, ownerId, roomId, boxDefinitionId, content) is not null;

    private uint? Open(uint itemId, int ownerId, uint roomId, uint boxDefinitionId, GiftContent content)
    {
        if (roomId == 0) {
            return null;
        }

        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@ownerId FOR UPDATE", new { ownerId }, transaction) is null) {
            return null;
        }

        if (connection.ExecuteScalar<uint?>("SELECT id FROM items WHERE id=@itemId AND user_id=@ownerId AND room_id=@roomId AND base_item=@boxDefinitionId FOR UPDATE",
                new { itemId, ownerId, roomId, boxDefinitionId }, transaction) is null) {
            return null;
        }

        var rows = connection.Query<ContentRow>("SELECT base_id AS BaseId,extra_data AS ExtraData,COALESCE(chat_style_id,0) AS ChatStyleId FROM user_presents WHERE item_id=@itemId FOR UPDATE", new { itemId }, transaction).ToArray();

        if (rows.Length != 1 || rows[0].BaseId != content.BaseId || rows[0].ExtraData != content.ExtraData || rows[0].ChatStyleId != content.ChatStyleId
            || !ValidPrize(connection, transaction, new RecyclerPrize(0, content.BaseId, content.ChatStyleId))) {
            return null;
        }

        if (connection.Execute("DELETE FROM user_presents WHERE item_id=@itemId", new { itemId }, transaction) != 1
            || connection.Execute("DELETE FROM items WHERE id=@itemId AND user_id=@ownerId AND room_id=@roomId AND base_item=@boxDefinitionId",
                new { itemId, ownerId, roomId, boxDefinitionId }, transaction) != 1) {
            throw new DBConcurrencyException("Recycler box changed during opening.");
        }

        uint rewardId;

        if (content.ChatStyleId > 0) {
            connection.Execute("INSERT IGNORE INTO user_purchased_chat_styles(user_id,style_id) VALUES(@ownerId,@styleId)",
                new { ownerId, styleId = content.ChatStyleId }, transaction);
            rewardId = 0;
        }
        else {
            connection.Execute("INSERT INTO items(base_item,user_id,room_id,x,y,z,wall_pos,rot,extra_data,limited_number,limited_stack) VALUES(@baseId,@ownerId,0,0,0,0,'',0,@extraData,0,0)",
                new { content.BaseId, ownerId, content.ExtraData }, transaction);
            rewardId = connection.ExecuteScalar<uint>("SELECT LAST_INSERT_ID()", transaction: transaction);
        }

        var reference = Guid.NewGuid();
        RecyclerReceiptStore.Opened(connection, transaction, reference, ownerId, itemId, rewardId, content.BaseId, content.ChatStyleId, (clock ?? TimeProvider.System).GetUtcNow());

        return RecyclerCommit.Complete(connection, transaction, () => Committed(ownerId, reference, opening: true))
            ? rewardId : null;
    }

    private bool Committed(int userId, Guid reference, bool opening)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        // Wait for the previous transaction before deciding whether its receipt exists.
        connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction);
        var table = opening ? "recycler_openings" : "recycler_operations";

        return connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {table} WHERE reference=@reference AND user_id=@userId",
            new { userId, reference = reference.ToString() }, transaction) == 1;
    }

    private static bool ValidPrize(IDbConnection connection, IDbTransaction transaction, RecyclerPrize prize) => prize.ChatStyleId > 0 && prize.ItemId == 0
        ? connection.ExecuteScalar<int?>("SELECT id FROM room_chat_styles WHERE id=@id AND enabled=1 AND requires_ownership=1 AND requires_hc=0 AND COALESCE(required_permission,'')='' LOCK IN SHARE MODE", new { id = prize.ChatStyleId }, transaction) is not null
        : prize.ChatStyleId == 0 && prize.ItemId > 0 && connection.ExecuteScalar<uint?>("SELECT id FROM furniture WHERE id=@id AND type IN ('s','i') AND sprite_id>0 LOCK IN SHARE MODE", new { id = prize.ItemId }, transaction) is not null;

    private static RecyclerConfiguration ReadConfiguration(IDbConnection connection, IDbTransaction? transaction = null)
    {
        var suffix = transaction is null ? "" : " LOCK IN SHARE MODE";
        var setting = connection.QuerySingleOrDefault<SettingsRow>("SELECT enabled,slots,cooldown_seconds AS CooldownSeconds,cost_duckets AS CostDuckets FROM recycler_settings WHERE id=1" + suffix, transaction: transaction);

        if (setting is null) {
            return RecyclerConfiguration.Closed;
        }

        var levels = connection.Query<LevelRow>("SELECT level,chance FROM recycler_levels ORDER BY level" + suffix, transaction: transaction).ToArray();
        var prizes = connection.Query<PrizeRow>("SELECT id,level,COALESCE(item_id,0) AS ItemId,COALESCE(chat_style_id,0) AS ChatStyleId FROM recycler_prizes ORDER BY level,id" + suffix, transaction: transaction).ToLookup(prize => prize.Level);

        // Orphan prizes also invalidate the dataset instead of silently changing its distribution.
        if (prizes.Any(group => !levels.Any(level => level.Level == group.Key))) {
            return RecyclerConfiguration.Closed;
        }

        return new(setting.Enabled, setting.Slots, setting.CooldownSeconds, levels.Select(level => new RecyclerLevel(level.Level, level.Chance,
            prizes[level.Level].Select(prize => new RecyclerPrize(prize.Id, prize.ItemId, prize.ChatStyleId)).ToImmutableArray())).ToImmutableArray(), setting.CostDuckets);
    }

    private sealed class SettingsRow
    {
        public bool Enabled { get; set; }
        public int Slots { get; set; }
        public int CooldownSeconds { get; set; }
        public int CostDuckets { get; set; }
    }
    private sealed class LevelRow
    {
        public int Level { get; set; }
        public int Chance { get; set; }
    }
    private sealed class PrizeRow
    {
        public int Id { get; set; }
        public int Level { get; set; }
        public uint ItemId { get; set; }
        public int ChatStyleId { get; set; }
    }
    private sealed class InputRow
    {
        public uint Id { get; set; }
        public uint ItemId { get; set; }
        public uint LimitedNumber { get; set; }
        public uint LimitedStack { get; set; }
    }
    private sealed class ContentRow
    {
        public uint BaseId { get; set; }
        public int ChatStyleId { get; set; }
        public string ExtraData { get; set; } = "";
    }
}
