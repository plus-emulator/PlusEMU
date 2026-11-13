using System.Data;
using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Marketplace;
using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

[Collection("Group purchase")]
public class MarketplaceOfferSearchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public void SearchPreservesTypedResultsWithoutWritingSharedMarketLists()
    {
        var (database, manager) = Fixture(Rows());
        var snapshot = new MarketplaceOfferSearchService(database, manager, new FixedClock(Now)).Search(-1, -1, "", 0);
        Assert.Equal(new uint[] { 2, 7, 5, 6 }, snapshot.Offers.Select(offer => offer.OfferId));
        Assert.Equal(2, snapshot.Offers[1].ItemType);
        Assert.Equal(new uint[] { 9, 3 }, snapshot.Offers.Where(offer => offer.LimitedNumber > 0).Select(offer => offer.LimitedNumber));
        Assert.Empty(manager.MarketItems);
        Assert.Empty(manager.MarketItemKeys);
    }

    [Fact]
    public void ConfiguredExpirationControlsTheSearchCutoff()
    {
        var (database, manager) = Fixture(Rows());
        var policy = new MarketplaceFeePolicy(new TestRoomSettings(new() { ["catalog.marketplace.expiration_hours"] = "168" }));
        new MarketplaceOfferSearchService(database, manager, new FixedClock(Now), policy).Search(-1, -1, "", 0);
        Assert.Equal(Now.AddDays(-7), Assert.Single(database.OfferQueries).Parameters["threshold"]);
    }

    [Theory]
    [InlineData(1, "DESC", -1, -1)]
    [InlineData(0, "ASC", 10, 500)]
    public async Task FilterModeChoosesFixedOrderAndBindsBounds(int mode, string order, int min, int max)
    {
        var (database, manager) = Fixture(Rows());
        var (client, _) = HabbiconTestSupport.Client(new Habbo { Id = 1, Username = "u" });

        await new GetOffersEvent(new MarketplaceOfferSearchService(database, manager, new FixedClock(Now))).Parse(client, Packet(min, max, "ignored", mode));

        var query = Assert.Single(database.OfferQueries);
        Assert.Contains($"ORDER BY r.total_price {order},r.offer_id LIMIT 500", query.Sql);
        Assert.DoesNotContain(min.ToString(), query.Sql);
        Assert.Equal(min, query.Parameters["minCost"]);
        Assert.Equal(max, query.Parameters["maxCost"]);
        Assert.Equal(Now.AddSeconds(-172800), query.Parameters["threshold"]);
        Assert.Equal("%ignored%", query.Parameters["pattern"]);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(1, 0)]
    [InlineData(2, 4)]
    public async Task OfferTypeKeepsOnlyFurniOffersBecauseNoOfferIsACollectible(int offerType, int expectedOffers)
    {
        var (database, manager) = Fixture(Rows());
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 1, Username = "u" });

        await new GetOffersEvent(new MarketplaceOfferSearchService(database, manager, new FixedClock(Now))).Parse(client, Packet(-1, -1, "", 0, true, offerType));

        var payload = sent.Single().Payload;
        Assert.Equal(expectedOffers, BinaryPrimitives.ReadInt32BigEndian(payload));
        Assert.Equal(expectedOffers, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(payload.Length - 4)));
    }

    [Fact]
    public async Task SnapshotDoesNotFollowLaterMarketStateMutation()
    {
        var (database, manager) = Fixture(Rows());
        var items = (List<MarketOffer>)manager.MarketItems;
        var snapshot = new MarketplaceOfferSearchService(database, manager, new FixedClock(Now)).Search(-1, -1, "", 0);
        var before = Writes(snapshot);

        items.Clear();
        ((List<int>)manager.MarketItemKeys).Clear();
        items.Add(new MarketOffer(9, 900, 1, 1, 0, 0));

        Assert.Equal(before, Writes(snapshot));
        Assert.Equal(4, snapshot.Offers.Length);
    }

    private static List<object[]> Rows() =>
    [
        [2u, "1", 100, 30, 0, 0], [7u, "2", 200, 20, 0, 0],
        [5u, "1", 300, 10, 9, 40], [6u, "1", 300, 5, 3, 40],
    ];

    private static (GroupManagementTests.RecordingDatabase Database, IMarketplaceManager Manager) Fixture(List<object[]>? rows)
    {
        var database = new GroupManagementTests.RecordingDatabase { OfferRows = Table(rows) };
        var items = new List<MarketOffer>();
        var keys = new List<int>();
        var manager = Proxy<IMarketplaceManager>((method, args) => method switch
        {
            "get_MarketItems" => items,
            "get_MarketItemKeys" => keys,
            "AvgPriceForSprite" => (int)args[0]! * 2,
            _ => throw new InvalidOperationException(method),
        });

        return (database, manager);
    }

    private static DataTable Table(List<object[]>? rows)
    {
        var table = new DataTable();
        table.Columns.Add("OfferId", typeof(uint));
        table.Columns.Add("ItemType", typeof(string));
        table.Columns.Add("SpriteId", typeof(int));
        table.Columns.Add("TotalPrice", typeof(int));
        table.Columns.Add("LimitedNumber", typeof(int));
        table.Columns.Add("LimitedStack", typeof(int));
        table.Columns.Add("ExtraData", typeof(string));
        table.Columns.Add("OfferCount", typeof(int));
        table.Columns.Add("TotalFound", typeof(int));
        table.Columns.Add("AveragePrice", typeof(int));

        foreach (var row in rows ?? []) {
            table.Rows.Add(row.Concat(new object[] { "", 1, rows!.Count, (int)row[2] * 2 }).ToArray());
        }

        return table;
    }

    private static string Writes(MarketplaceOffersSnapshot snapshot)
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new MarketPlaceOffersComposer(snapshot).Compose(packet);

        return string.Join("|", packet.Writes.Select(write => $"{write.GetType().Name}:{write}"));
    }

    [Fact]
    public void SearchTreatsWildcardsAsLiteralTextAndDoesNotInterpolateTheQuery()
    {
        var (database, manager) = Fixture([]);
        new MarketplaceOfferSearchService(database, manager, new FixedClock(Now)).Search(-1, -1, "50%_! ' OR 1=1", 0);
        var query = Assert.Single(database.OfferQueries);
        Assert.Equal("%50!%!_!! ' OR 1=1%", query.Parameters["pattern"]);
        Assert.DoesNotContain("OR 1=1", query.Sql);
    }

    [Fact]
    public void OversizedSearchDoesNotReachTheDatabase()
    {
        var (database, manager) = Fixture([]);
        Assert.Empty(new MarketplaceOfferSearchService(database, manager, new FixedClock(Now)).Search(-1, -1, new string('x', 101), 0).Offers);
        Assert.Empty(database.OfferQueries);
    }

    [Fact]
    public void OfferWireSeparatesFloorStuffDataWallDataAndLimitedSerials()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new MarketPlaceOffersComposer(new([
            new(1, 10, 0, 0, 5, 4, 2, 1, "3"),
            new(2, 10, 0, 0, 7, 6, 1, 2, "poster"),
            new(3, 10, 9, 100, 50, 40, 1)
        ], 700)).Compose(packet);
        Assert.Equal(new object[] {
            3,
            1u, 1, 1, 10u, 0, "3", 5, 0, 4, 2,
            2u, 1, 2, 10u, "poster", 7, 0, 6, 1,
            3u, 1, 3, 10u, 9u, 100u, 50, 0, 40, 1,
            700
        }, packet.Writes);
    }

    [Fact]
    public void StatisticsWireIncludesDailyHistoryBeforeTheItemIdentity()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new MarketplaceItemStatsComposer(2, 55, new MarketplaceItemStats(150, 3,
            [new(-30, 90, 2), new(0, 150, 4)], 99, 0)).Compose(packet);
        Assert.Equal(new object[] { 150, 3, 30, 2, -30, 90, 2, 0, 150, 4, 2, 55u, 99, 0 }, packet.Writes);
    }

    private static FlashIncomingPacket Packet(params object[] values)
    {
        using var stream = new MemoryStream();

        foreach (var value in values) {
            if (value is int number) {
                var bytes = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(bytes, number);
                stream.Write(bytes);
            }
            else if (value is string text) {
                var raw = Encoding.UTF8.GetBytes(text);
                var length = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)raw.Length);
                stream.Write(length);
                stream.Write(raw);
            }
            else if (value is bool flag) {
                stream.WriteByte(flag ? (byte)1 : (byte)0);
            }
        }

        return new FlashIncomingPacket { Buffer = stream.ToArray() };
    }

    private static T Proxy<T>(Func<string, object?[], object?> call) where T : class => CatalogSnapshotTestSupport.Proxy<T>(call);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
