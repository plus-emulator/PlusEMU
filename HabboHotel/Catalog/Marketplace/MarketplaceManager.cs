using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Database;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.Utilities;

namespace Plus.HabboHotel.Catalog.Marketplace;

public class MarketplaceManager : IMarketplaceManager
{
    private readonly IDatabase _database;
    private readonly IItemDataManager _itemDataManager;
    private readonly IItemFactory _itemFactory;
    private readonly TimeProvider _time;
    private readonly IMarketplaceFeePolicy? _policy;
    public Dictionary<int, int> MarketAverages { get; } = new();
    public Dictionary<int, int> MarketCounts { get; } = new();
    public List<int> MarketItemKeys { get; } = new();
    public List<MarketOffer> MarketItems { get; } = new();

    public MarketplaceManager(IDatabase database, IItemDataManager itemDataManager, IItemFactory itemFactory, TimeProvider time, IMarketplaceFeePolicy? policy = null)
    {
        _database = database;
        _itemDataManager = itemDataManager;
        _itemFactory = itemFactory;
        _time = time;
        _policy = policy;
    }
    public int AvgPriceForSprite(int spriteId)
    {
        var num = 0;
        var num2 = 0;

        if (MarketAverages.ContainsKey(spriteId) && MarketCounts.ContainsKey(spriteId)) {
            if (MarketCounts[spriteId] > 0) {
                return MarketAverages[spriteId] / MarketCounts[spriteId];
            }

            return 0;
        }

        using var connection = _database.Connection();
        var totals = connection.QuerySingleOrDefault<MarketTotals>(
            "SELECT avgprice AS AveragePrice,sold AS Sold FROM catalog_marketplace_data WHERE sprite=@spriteId LIMIT 1", new { spriteId });
        num = totals?.AveragePrice ?? 0;
        num2 = totals?.Sold ?? 0;
        MarketAverages.Add(spriteId, num);
        MarketCounts.Add(spriteId, num2);

        if (num2 > 0) {
            return Convert.ToInt32(Math.Ceiling((double)(num / num2)));
        }

        return 0;
    }

    public int OfferCountForSprite(uint spriteId)
    {
        var dictionary = new Dictionary<uint, MarketOffer>();
        var dictionary2 = new Dictionary<uint, int>();

        foreach (var item in MarketItems) {
            if (dictionary.ContainsKey(item.SpriteId)) {
                if (dictionary[item.SpriteId].TotalPrice > item.TotalPrice) {
                    dictionary.Remove(item.SpriteId);
                    dictionary.Add(item.SpriteId, item);
                }

                var num = dictionary2[item.SpriteId];
                dictionary2.Remove(item.SpriteId);
                dictionary2.Add(item.SpriteId, num + 1);
            }
            else {
                dictionary.Add(item.SpriteId, item);
                dictionary2.Add(item.SpriteId, 1);
            }
        }

        if (dictionary2.ContainsKey(spriteId)) {
            return dictionary2[spriteId];
        }

        return 0;
    }

    public MarketplaceItemStats ItemStats(uint spriteId) => ItemStats(1, spriteId);

    public MarketplaceItemStats ItemStats(int itemType, uint spriteId) =>
        MarketplaceStatistics.Read(_database, _time, itemType, spriteId, _policy?.OfferExpirationHours ?? 48);

    public MarketplaceOwnOffers OwnOffers(int userId)
    {
        using var connection = _database.Connection();
        var rows = connection.Query<OwnOfferRow>(
            """
            SELECT o.listed_at AS ListedAt,o.state AS State,o.offer_id AS OfferId,o.sprite_id AS SpriteId,o.total_price AS TotalPrice,
                o.limited_number AS LimitedNumber,o.limited_stack AS LimitedStack,o.item_type AS ItemType,o.extra_data AS ExtraData,
                COALESCE(a.average_price,0) AS AveragePrice,s.sold_at AS SoldAt
            FROM catalog_marketplace_offers o LEFT JOIN (
                SELECT sprite_id,item_type,LEAST(2147483647,CEILING(SUM(total_price)/NULLIF(SUM(sold),0))) AS average_price
                FROM marketplace_daily_sales WHERE sale_date>=@averageStart AND sale_date<=@today GROUP BY item_type,sprite_id
            ) a ON a.sprite_id=o.sprite_id AND a.item_type=CASE WHEN o.limited_number>0 THEN 3 ELSE o.item_type END
            LEFT JOIN marketplace_sales s ON s.offer_id=o.offer_id WHERE o.user_id=@userId ORDER BY o.offer_id
            """,
            new { userId, averageStart = _time.GetUtcNow().UtcDateTime.Date.AddDays(-6), today = _time.GetUtcNow().UtcDateTime.Date }).ToArray();
        var accumulated = connection.ExecuteScalar<int?>(
            "SELECT SUM(`asking_price`) FROM `catalog_marketplace_offers` WHERE `state` = 2 AND `user_id` = @userId",
            new { userId }) ?? 0;
        var now = _time.GetUtcNow();
        var offers = rows.Select(row =>
        {
            var minutes = MinutesRemaining(row.ListedAt, now, _policy?.OfferExpirationHours ?? 48);
            // The state column is an enum of '1' (listed) and '2' (sold); an unsold offer past its lifetime is shown as expired.
            var state = int.Parse(row.State);

            if (minutes <= 0 && state != 2) {
                state = 3;
                minutes = 0;
            }

            return new MarketplaceOwnOffer(Convert.ToInt32(row.OfferId), state, row.SpriteId, row.LimitedNumber, row.LimitedStack,
                row.TotalPrice, minutes, row.ItemType, row.ExtraData, row.AveragePrice, row.SoldAt?.ToUnixTimeMilliseconds() ?? 0);
        }).ToArray();

        return new(accumulated, offers);
    }

    // Column types as MariaDB stores them; the state enum arrives as text.
    private sealed class OwnOfferRow
    {
        public DateTimeOffset? ListedAt { get; set; }
        public string State { get; set; } = "";
        public uint OfferId { get; set; }
        public int SpriteId { get; set; }
        public int TotalPrice { get; set; }
        public int LimitedNumber { get; set; }
        public int LimitedStack { get; set; }
        public int ItemType { get; set; }
        public string ExtraData { get; set; } = "";
        public int AveragePrice { get; set; }
        public DateTimeOffset? SoldAt { get; set; }
    }

    // A NULL listing time is unknown and counts as expired; far-future listings clamp to the int wire range instead of overflowing.
    private static int MinutesRemaining(DateTimeOffset? listedAt, DateTimeOffset now, int expirationHours)
    {
        if (listedAt is not { } listed) {
            return 0;
        }

        // Epoch arithmetic instead of DateTimeOffset: a listing in the last days of year 9999 would overflow adding its lifetime.
        var expiresAt = (listed - DateTimeOffset.UnixEpoch).TotalSeconds + expirationHours * 3600d;
        var minutes = Math.Floor((expiresAt - (now - DateTimeOffset.UnixEpoch).TotalSeconds) / 60.0);

        return (int)Math.Clamp(minutes, int.MinValue, int.MaxValue);
    }

    public Task<bool> TryCancelOffer(Habbo habbo, uint offerId)
    {
        lock (habbo.InventoryMutationSync)
            lock (habbo.WalletSync) {
                if (habbo.WalletClosed || habbo.Inventory is not { } inventory) {
                    return Task.FromResult(false);
                }

                var returned = MarketplaceCommit.Guard(habbo, () => new MarketplaceCancellationStore(_database).Cancel(habbo.Id, offerId,
                    id => _itemDataManager.Items.GetValueOrDefault(id), delivery =>
                    {
                        var item = new Item
                        {
                            Id = delivery.FurniId,
                            OwnerId = (uint)habbo.Id,
                            Definition = delivery.Definition,
                            ExtraData = FurniExtraData.Load(delivery.Definition, delivery.ExtraData, keepLegacy: true),
                            UniqueNumber = delivery.LimitedNumber,
                            UniqueSeries = delivery.LimitedStack
                        };

                        return item.ToInventoryItem() ?? throw new InvalidOperationException("Marketplace return could not be prepared.");
                    }));

                if (returned is null) {
                    return Task.FromResult(false);
                }

                inventory.Furniture.AddItem(returned);
                habbo.Client?.Send(new FurniListAddComposer(InventoryItemSnapshot.Capture(returned)));
                habbo.Client?.Send(new FurniListNotificationComposer(returned.Id, returned.IsFloorItem ? 1 : 2));
                habbo.Client?.Send(new FurniListUpdateComposer());

                return Task.FromResult(true);
            }
    }

    public async Task<MarketOffer?> GetOffer(uint offerId)
    {
        using var connection = _database.Connection();

        return await connection.QuerySingleOrDefaultAsync<MarketOffer>(
            "SELECT `furni_id`, `sprite_id`, `item_id`, `user_id`, `extra_data`, `offer_id`, `state`, `limited_number`, `limited_stack` FROM `catalog_marketplace_offers` WHERE `offer_id` = @offerId LIMIT 1",
            new { offerId });
    }

    public async Task DeleteOffer(uint offerId)
    {
        using var connection = _database.Connection();
        await connection.ExecuteAsync("DELETE FROM `catalog_marketplace_offers` WHERE `offer_id` = @offerId LIMIT 1", new { offerId });
    }

    private sealed record MarketTotals(int AveragePrice, int Sold);
}
