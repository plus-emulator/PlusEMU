using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.Marketplace;

public sealed record MarketplaceListing(uint FurniId, uint ItemId, int UserId, int AskingPrice, int TotalPrice, string PublicName, int SpriteId,
    string ItemType, DateTimeOffset ListedAt, string ExtraData, uint LimitedNumber, uint LimitedStack);

public sealed record MarketplaceSoldOffer(uint OfferId, int AskingPrice);

public interface IMarketplaceOfferStore
{
    // Writes the offer and removes the furni row in one transaction. Throws, with nothing committed, unless exactly one furni row was removed.
    bool ListFurni(MarketplaceListing listing);

    // The same for a batch: every offer and every removal commits together, or nothing does (false when one of the furni is on a music playlist).
    bool ListFurni(IReadOnlyList<MarketplaceListing> listings);

    // Claims the user's sold offers in one transaction. Returns the user's new credit balance, or null (nothing changed) when nothing
    // was sold, a claim is negative, the total overflows, or balanceAfter has no balance for the total.
    int? ClaimSold(int userId, Func<int, int?> balanceAfter);
}

public sealed class MarketplaceOfferStore(IDatabase database) : IMarketplaceOfferStore
{
    public bool ListFurni(MarketplaceListing listing) => ListFurni([listing]);

    public bool ListFurni(IReadOnlyList<MarketplaceListing> listings)
    {
        if (listings.Count == 0 || listings.Select(listing => listing.UserId).Distinct().Count() != 1
            || listings.Any(listing => listing.TotalPrice <= 0 || listing.AskingPrice < 0 || listing.AskingPrice > listing.TotalPrice)) {
            return false;
        }

        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try {
            foreach (var userId in listings.Select(listing => listing.UserId).Distinct().Order()) {
                MarketplaceCommit.LockUser(connection, transaction, userId);
            }

            foreach (var listing in listings.OrderBy(listing => listing.FurniId)) {
                if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM room_music_playlist WHERE player_id=@FurniId OR disc_id=@FurniId",
                    new { listing.FurniId }, transaction) != 0) {
                    return false;
                }

                connection.Execute("INSERT INTO `catalog_marketplace_offers` (`furni_id`,`item_id`,`user_id`,`asking_price`,`total_price`,`public_name`,`sprite_id`,`item_type`,`listed_at`,`extra_data`,`limited_number`,`limited_stack`) " +
                    "VALUES (@FurniId,@ItemId,@UserId,@AskingPrice,@TotalPrice,@PublicName,@SpriteId,@ItemType,@ListedAt,@ExtraData,@LimitedNumber,@LimitedStack)",
                    new
                    {
                        listing.FurniId,
                        listing.ItemId,
                        listing.UserId,
                        listing.AskingPrice,
                        listing.TotalPrice,
                        listing.PublicName,
                        listing.SpriteId,
                        listing.ItemType,
                        ListedAt = listing.ListedAt.UtcDateTime,
                        listing.ExtraData,
                        listing.LimitedNumber,
                        listing.LimitedStack
                    }, transaction);
                // TODO @80O: Do not delete items from the items table when putting on marketplace. Just reference the furniture instead.
                var removed = connection.Execute("DELETE FROM `items` WHERE `id` = @FurniId AND `user_id` = @UserId AND `room_id` = 0 AND base_item=@ItemId LIMIT 1",
                    new { listing.FurniId, listing.UserId, listing.ItemId }, transaction);

                if (removed != 1) {
                    throw new InvalidOperationException($"Furni {listing.FurniId} was not removed from user {listing.UserId}; the offer was not published.");
                }
            }

            MarketplaceCommit.Complete(database, connection, transaction, listings[0].UserId, "list");

            return true;
        }
        catch {
            MarketplaceCommit.Rollback(transaction);
            throw;
        }
    }

    public int? ClaimSold(int userId, Func<int, int?> balanceAfter)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        try {
            MarketplaceCommit.LockUser(connection, transaction, userId);
            var sold = connection.Query<MarketplaceSoldOffer>("SELECT `offer_id` AS OfferId, `asking_price` AS AskingPrice FROM `catalog_marketplace_offers` " +
                "WHERE `user_id` = @userId AND `state` = '2' FOR UPDATE", new { userId }, transaction).ToList();
            long owed = 0;

            foreach (var offer in sold) {
                if (offer.AskingPrice < 0) {
                    transaction.Rollback();

                    return null;
                }

                owed += offer.AskingPrice;
            }

            if (sold.Count == 0 || owed > int.MaxValue || balanceAfter((int)owed) is not { } credits) {
                transaction.Rollback();

                return null;
            }

            var deleted = connection.Execute("DELETE FROM `catalog_marketplace_offers` WHERE `user_id` = @userId AND `state` = '2' AND `offer_id` IN @offerIds",
                new { userId, offerIds = sold.Select(offer => offer.OfferId).ToArray() }, transaction);

            if (deleted != sold.Count) {
                throw new InvalidOperationException($"Claimed {sold.Count} sold offers for user {userId} but removed {deleted}; nothing was paid.");
            }

            if (connection.Execute("UPDATE users SET credits = @credits WHERE id = @userId LIMIT 1", new { credits, userId }, transaction) != 1) {
                throw new InvalidOperationException($"User {userId} was not paid; the sold offers stay queued.");
            }

            MarketplaceCommit.Complete(database, connection, transaction, userId, "redeem");

            return credits;
        }
        catch {
            MarketplaceCommit.Rollback(transaction);
            throw;
        }
    }
}
