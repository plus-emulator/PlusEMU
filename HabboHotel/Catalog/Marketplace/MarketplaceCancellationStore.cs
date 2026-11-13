using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Catalog.Marketplace;

public sealed class MarketplaceCancellationStore(IDatabase database)
{
    public InventoryItem? Cancel(int ownerId, uint offerId, Func<uint, ItemDefinition?> definitionOf,
        Func<MarketplaceDelivery, InventoryItem> prepare, DateTimeOffset? expiredBefore = null)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        MarketplaceCommit.LockUser(connection, transaction, ownerId);
        var row = connection.QuerySingleOrDefault<OfferRow>(
            "SELECT state AS State,user_id AS UserId,furni_id AS FurniId,item_id AS ItemId,extra_data AS ExtraData," +
            "limited_number AS LimitedNumber,limited_stack AS LimitedStack,listed_at AS ListedAt FROM catalog_marketplace_offers WHERE offer_id=@offerId FOR UPDATE",
            new { offerId }, transaction);

        if (row is null || row.UserId != ownerId || row.State != "1" || row.FurniId == 0
            || row.LimitedNumber < 0 || row.LimitedStack < 0 || definitionOf(row.ItemId) is not { } definition
            || expiredBefore is { } cutoff && row.ListedAt >= cutoff) {
            return null;
        }

        var delivery = new MarketplaceDelivery(row.FurniId, row.ItemId, row.ExtraData,
            (uint)row.LimitedNumber, (uint)row.LimitedStack, definition);
        var item = prepare(delivery);
        connection.Execute("INSERT INTO items(id,base_item,user_id,room_id,x,y,z,wall_pos,rot,extra_data,limited_number,limited_stack) " +
            "VALUES(@FurniId,@ItemId,@ownerId,0,0,0,0,'',0,@ExtraData,@LimitedNumber,@LimitedStack)",
            new { delivery.FurniId, delivery.ItemId, ownerId, delivery.ExtraData, delivery.LimitedNumber, delivery.LimitedStack }, transaction);

        if (connection.Execute("DELETE FROM catalog_marketplace_offers WHERE offer_id=@offerId AND user_id=@ownerId AND state='1'",
            new { offerId, ownerId }, transaction) != 1) {
            throw new InvalidOperationException("Marketplace offer changed during cancellation.");
        }

        MarketplaceCommit.Complete(database, connection, transaction, ownerId, "cancel");

        return item;
    }

    private sealed class OfferRow
    {
        public string State { get; set; } = "";
        public int UserId { get; set; }
        public uint FurniId { get; set; }
        public uint ItemId { get; set; }
        public string ExtraData { get; set; } = "";
        public int LimitedNumber { get; set; }
        public int LimitedStack { get; set; }
        public DateTimeOffset? ListedAt { get; set; }
    }
}
