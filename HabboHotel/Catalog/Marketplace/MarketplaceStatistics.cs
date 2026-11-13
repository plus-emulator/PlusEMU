using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Catalog.Marketplace;

public sealed record MarketplaceHistoryDay(int DayOffset, int AveragePrice, int SoldAmount);

public static class MarketplaceStatistics
{
    public static MarketplaceItemStats Read(IDatabase database, TimeProvider time, int itemType, uint spriteId, int expirationHours)
    {
        if (itemType is not (1 or 2 or 3) || spriteId == 0) {
            return new(0, 0);
        }

        var now = time.GetUtcNow();
        var today = now.UtcDateTime.Date;
        using var connection = database.Connection();
        using var result = connection.QueryMultiple("""
            SELECT LEAST(2147483647,COUNT(*)) AS OfferCount,COALESCE(MIN(total_price),0) AS LowestPrice
            FROM catalog_marketplace_offers
            WHERE item_type=@storedType AND sprite_id=@spriteId AND state='1' AND listed_at>=@threshold AND total_price>0
                AND ((@itemType=3 AND limited_number>0) OR (@itemType<>3 AND limited_number=0));
            SELECT sale_date AS SaleDate,sold AS Sold,total_price AS TotalPrice FROM marketplace_daily_sales
            WHERE item_type=@itemType AND sprite_id=@spriteId AND sale_date>=@start AND sale_date<=@today
            ORDER BY sale_date;
            """, new { itemType, storedType = itemType == 3 ? 1 : itemType, spriteId, threshold = now.AddHours(-expirationHours), start = today.AddDays(-30), today });
        var live = result.ReadSingle<Live>();
        var days = result.Read<Day>().ToArray();
        var recent = days.Where(day => day.SaleDate >= today.AddDays(-6)).ToArray();
        var count = recent.Sum(day => day.Sold);
        var average = count > 0 ? Wire(decimal.Ceiling(recent.Sum(day => day.TotalPrice) / count)) : 0;

        return new(average, live.OfferCount, days.Select(day => new MarketplaceHistoryDay(
            (day.SaleDate.Date - today).Days, day.Sold > 0 ? Wire(decimal.Ceiling(day.TotalPrice / day.Sold)) : 0, Wire(day.Sold))).ToArray(),
            live.LowestPrice, 0);
    }

    private static int Wire(decimal value) => (int)Math.Clamp(value, 0, int.MaxValue);
    private sealed class Live
    {
        public int OfferCount { get; set; }
        public int LowestPrice { get; set; }
    }
    private sealed class Day
    {
        public DateTime SaleDate { get; set; }
        public decimal Sold { get; set; }
        public decimal TotalPrice { get; set; }
    }
}
