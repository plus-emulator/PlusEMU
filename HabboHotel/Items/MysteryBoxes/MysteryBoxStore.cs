using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items.MysteryBoxes;

public sealed class MysteryBoxStore(IDatabase database, IItemDataManager items) : IMysteryBoxStore
{
    public MysteryBoxColors Colors(int userId)
    {
        using var connection = database.Connection();

        return connection.QuerySingleOrDefault<MysteryBoxColors>(
            "SELECT box_color AS Box, key_color AS `Key` FROM user_mystery_boxes WHERE user_id=@userId", new { userId }) ?? MysteryBoxColors.None;
    }

    public IReadOnlyList<MysteryBoxPrize> Prizes()
    {
        using var connection = database.Connection();

        return connection.Query<(uint FurnitureId, int Weight)>("SELECT furniture_id, weight FROM mystery_box_prizes")
            .Where(row => items.Items.ContainsKey(row.FurnitureId) && items.Items[row.FurnitureId].ProductType is "s" or "i")
            .Select(row => new MysteryBoxPrize(items.Items[row.FurnitureId], row.Weight))
            .ToList();
    }

    public MysteryBoxOpened? Open(MysteryBoxOpening opening)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        var key = connection.ExecuteScalar<string?>("SELECT key_color FROM user_mystery_boxes WHERE user_id=@guestId FOR UPDATE",
            new { guestId = opening.GuestId }, transaction);

        if (key == null || !string.Equals(key, opening.KeyColor, StringComparison.Ordinal)) {
            return null;
        }

        connection.Execute("UPDATE user_mystery_boxes SET key_color='' WHERE user_id=@guestId", new { guestId = opening.GuestId }, transaction);
        connection.Execute("UPDATE items SET extra_data=@state WHERE id=@boxId", new { state = opening.OpenedState, boxId = opening.BoxItemId }, transaction);
        var ownerPrize = Insert(connection, transaction, opening.OwnerId, opening.OwnerPrize);
        var guestPrize = Insert(connection, transaction, opening.GuestId, opening.GuestPrize);
        transaction.Commit();

        return new(ownerPrize, guestPrize);
    }

    private static InventoryItem Insert(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, ItemDefinition prize)
    {
        var id = connection.ExecuteScalar<uint>("INSERT INTO items(user_id,base_item,extra_data) VALUES(@userId,@baseId,''); SELECT LAST_INSERT_ID()",
            new { userId, baseId = prize.Id }, transaction);

        return new() { Id = id, OwnerId = checked((uint)userId), Definition = prize, ExtraData = FurniExtraData.Load(prize, "", keepLegacy: true) };
    }
}
