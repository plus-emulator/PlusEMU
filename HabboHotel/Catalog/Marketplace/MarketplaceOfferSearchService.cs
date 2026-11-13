using System.Collections.Immutable;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.Marketplace;

public sealed record MarketplaceOfferEntry(uint OfferId, uint SpriteId, uint LimitedNumber, uint LimitedStack, int TotalPrice, int AveragePrice, int Count,
    int ItemType = 1, string ExtraData = "");

public sealed record MarketplaceOffersSnapshot(ImmutableArray<MarketplaceOfferEntry> Offers, int? TotalFound = null);

public interface IMarketplaceOfferSearchService
{
    MarketplaceOffersSnapshot Search(int minCost, int maxCost, string searchQuery, int filterMode);
    MarketplaceOffersSnapshot Capture(IReadOnlyDictionary<uint, MarketOffer> best, IReadOnlyDictionary<uint, int> counts);
}

public sealed record MarketplaceOfferRow(uint OfferId, string ItemType, int SpriteId, int TotalPrice, int LimitedNumber, int LimitedStack);

public sealed class MarketplaceOfferSearchService(IDatabase database, IMarketplaceManager marketplace, TimeProvider time, IMarketplaceFeePolicy? policy = null) : IMarketplaceOfferSearchService
{
    private const string SelectOffers = """
        WITH ranked AS (
            SELECT offer_id,item_type,sprite_id,total_price,limited_number,limited_stack,extra_data,
                ROW_NUMBER() OVER (PARTITION BY item_type,sprite_id,CASE WHEN limited_number>0 THEN offer_id ELSE 0 END
                    ORDER BY total_price,offer_id) AS position,
                COUNT(*) OVER (PARTITION BY item_type,sprite_id,CASE WHEN limited_number>0 THEN offer_id ELSE 0 END) AS offer_count
            FROM `catalog_marketplace_offers`
            WHERE state='1' AND listed_at>=@threshold AND total_price>0 AND sprite_id>0 AND item_type IN ('1','2')
                AND ((limited_number=0 AND limited_stack=0) OR (item_type='1' AND limited_number BETWEEN 1 AND limited_stack))
                AND (@pattern='' OR public_name LIKE @pattern ESCAPE '!')
        )
        SELECT r.offer_id AS OfferId,r.item_type AS ItemType,r.sprite_id AS SpriteId,r.total_price AS TotalPrice,
            r.limited_number AS LimitedNumber,r.limited_stack AS LimitedStack,r.extra_data AS ExtraData,
            r.offer_count AS OfferCount,COUNT(*) OVER () AS TotalFound,COALESCE(a.average_price,0) AS AveragePrice
        FROM ranked r LEFT JOIN (
            SELECT sprite_id,item_type,LEAST(2147483647,CEILING(SUM(total_price)/NULLIF(SUM(sold),0))) AS average_price
            FROM marketplace_daily_sales WHERE sale_date>=@averageStart AND sale_date<=@today GROUP BY item_type,sprite_id
        ) a ON a.sprite_id=r.sprite_id AND a.item_type=CASE WHEN r.limited_number>0 THEN 3 ELSE r.item_type END
        WHERE r.position=1 AND (@minCost<0 OR r.total_price>=@minCost) AND (@maxCost<0 OR r.total_price<=@maxCost)
        """;

    public MarketplaceOffersSnapshot Search(int minCost, int maxCost, string searchQuery, int filterMode)
    {
        var sql = SelectOffers + (filterMode == 1 ? " ORDER BY r.total_price DESC,r.offer_id LIMIT 500" : " ORDER BY r.total_price ASC,r.offer_id LIMIT 500");
        // Expired offers must disappear from search and be refused by purchases at the same cutoff.
        var threshold = time.GetUtcNow().AddHours(-(policy?.OfferExpirationHours ?? 48));

        if (searchQuery.Length > 100) {
            return new([]);
        }

        var text = searchQuery.Trim();
        var pattern = text.Length == 0 ? "" : "%" + text.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_") + "%";

        using (var connection = database.Connection()) {
            var rows = connection.Query<SearchRow>(sql, new { threshold, minCost, maxCost, pattern, averageStart = time.GetUtcNow().UtcDateTime.Date.AddDays(-6), today = time.GetUtcNow().UtcDateTime.Date }).ToArray();

            return new(rows.Select(row => new MarketplaceOfferEntry(row.OfferId, row.SpriteId, row.LimitedNumber, row.LimitedStack,
                row.TotalPrice, row.AveragePrice, row.OfferCount, row.ItemType, row.ExtraData)).ToImmutableArray(), rows.FirstOrDefault()?.TotalFound ?? 0);
        }
    }

    public MarketplaceOffersSnapshot Capture(IReadOnlyDictionary<uint, MarketOffer> best, IReadOnlyDictionary<uint, int> counts) => new(
        best.Values.Select(offer => new MarketplaceOfferEntry(offer.OfferId, offer.SpriteId, offer.LimitedNumber, offer.LimitedStack, offer.TotalPrice,
            marketplace.AvgPriceForSprite((int)offer.SpriteId), counts.GetValueOrDefault(offer.OfferId, counts.GetValueOrDefault(offer.SpriteId)), offer.ItemType)).ToImmutableArray());

    private sealed class SearchRow
    {
        public uint OfferId { get; set; }
        public uint SpriteId { get; set; }
        public uint LimitedNumber { get; set; }
        public uint LimitedStack { get; set; }
        public int TotalPrice { get; set; }
        public int AveragePrice { get; set; }
        public int OfferCount { get; set; }
        public int TotalFound { get; set; }
        public int ItemType { get; set; }
        public string ExtraData { get; set; } = "";
    }
}
