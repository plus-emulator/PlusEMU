using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;

namespace Plus.HabboHotel.Recycler;

internal static class RecyclerReceiptStore
{
    public static void Recycled(IDbConnection connection, IDbTransaction transaction, Guid reference, int userId, uint boxId,
        RecyclerConfiguration configuration, RecyclerPrize prize, IReadOnlyList<RecyclerInput> inputs, int? balance, DateTimeOffset now)
    {
        var json = JsonSerializer.Serialize(configuration);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var level = configuration.Levels.Single(level => level.Prizes.Contains(prize)).Id;
        var ids = inputs.Select(input => input.Id).ToArray();
        var definitions = inputs.Select(input => input.ItemId).Distinct().ToArray();
        var historical = connection.Query<uint>("SELECT reward_item_id FROM recycler_openings WHERE reward_item_id IN @ids",
            new { ids }, transaction).ToHashSet();
        var current = connection.Query<uint>("SELECT DISTINCT item_id FROM recycler_prizes WHERE item_id IN @definitions",
            new { definitions }, transaction).ToHashSet();
        var recycledRewards = inputs.Count(input => historical.Contains(input.Id) || current.Contains(input.ItemId));
        connection.Execute("""
            INSERT IGNORE INTO recycler_revisions(hash,configuration_json,created_at) VALUES(@hash,@json,@now);
            INSERT INTO recycler_operations(reference,user_id,box_item_id,configuration_hash,prize_id,prize_level,
                prize_item_id,chat_style_id,inputs_json,cost_duckets,balance_after,created_at)
            VALUES(@reference,@userId,@boxId,@hash,@prizeId,@level,@itemId,@style,@inputs,@cost,@balance,@now);
            INSERT INTO recycler_progress(user_id,recycled_items,recycled_rewards) VALUES(@userId,@count,@recycledRewards)
            ON DUPLICATE KEY UPDATE recycled_items=recycled_items+@count,recycled_rewards=recycled_rewards+@recycledRewards;
            """, new
        {
            hash,
            json,
            reference = reference.ToString(),
            userId,
            boxId,
            prizeId = prize.Id,
            level,
            itemId = prize.ItemId,
            style = prize.ChatStyleId,
            inputs = JsonSerializer.Serialize(inputs),
            cost = configuration.CostDuckets,
            balance,
            now = now.UtcDateTime,
            count = inputs.Count,
            recycledRewards
        }, transaction);
    }

    public static void Opened(IDbConnection connection, IDbTransaction transaction, Guid reference, int userId, uint boxId,
        uint rewardId, uint definitionId, int styleId, DateTimeOffset now)
    {
        var operation = connection.QuerySingleOrDefault<Operation>("""
            SELECT reference,prize_level AS Level,prize_item_id AS ItemId,chat_style_id AS StyleId
            FROM recycler_operations WHERE box_item_id=@boxId
            """, new { boxId }, transaction);

        if (operation is not null && (operation.ItemId != definitionId || operation.StyleId != styleId)) {
            throw new DBConcurrencyException("Recycler box content does not match its receipt.");
        }

        var level = operation?.Level ?? 0;
        connection.Execute("""
            INSERT INTO recycler_openings(reference,operation_reference,box_item_id,user_id,prize_level,reward_item_id,
                reward_definition_id,chat_style_id,opened_at)
            VALUES(@reference,@operation,@boxId,@userId,@level,@rewardId,@definitionId,@styleId,@now);
            INSERT INTO recycler_progress(user_id,boxes_opened,rare_prizes,grand_prizes) VALUES(@userId,1,@rare,@grand)
            ON DUPLICATE KEY UPDATE boxes_opened=boxes_opened+1,rare_prizes=rare_prizes+@rare,grand_prizes=grand_prizes+@grand;
            """, new
        {
            reference = reference.ToString(),
            operation = operation?.Reference,
            userId,
            boxId,
            level,
            rewardId = rewardId == 0 ? (uint?)null : rewardId,
            definitionId,
            styleId,
            now = now.UtcDateTime,
            rare = level >= 3 ? 1 : 0,
            grand = level == 6 ? 1 : 0
        }, transaction);
    }

    private sealed class Operation
    {
        public Guid Reference { get; set; }
        public int Level { get; set; }
        public uint ItemId { get; set; }
        public int StyleId { get; set; }
    }
}
