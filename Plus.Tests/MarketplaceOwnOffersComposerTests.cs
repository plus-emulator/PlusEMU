using Xunit;
using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

public class MarketplaceOwnOffersComposerTests
{
    [Fact]
    public void SerializesPreparedOffersAndSnapshotsMutableInput()
    {
        var offers = new[]
        {
            new MarketplaceOwnOffer(10, 1, 100, 0, 0, 500, 60),
            new MarketplaceOwnOffer(11, 2, 101, 0, 0, 600, -4, ItemType: 2, ExtraData: "poster", AveragePrice: 520, StatusTime: 4294967301),
            new MarketplaceOwnOffer(12, 3, 102, 7, 20, 700, 0, StatusTime: 8589934600)
        };
        var composer = new MarketPlaceOwnOffersComposer(new MarketplaceOwnOffers(900, offers));
        offers[0] = new MarketplaceOwnOffer(99, 9, 999, 9, 9, 9, 9);

        var first = new RecordingPacket();
        var second = new RecordingPacket();
        composer.Compose(first);
        composer.Compose(second);

        Assert.Equal(first.Writes, second.Writes);
        Assert.Equal(new object[]
        {
            900, 3,
            10, 1, 1, 100, 0, "", 500, 60, 0,
            11, 2, 2, 101, "poster", 600, -4, 520, 1, 5,
            12, 3, 3, 102, 7, 20, 700, 0, 0, 2, 8
        }, first.Writes);
    }

    private sealed class RecordingPacket : IOutgoingPacket
    {
        public List<object> Writes { get; } = [];
        public int MessageId { get; set; }
        public ReadOnlyMemory<byte> Buffer => ReadOnlyMemory<byte>.Empty;
        public void WriteByte(byte value) => Writes.Add(value);
        public void WriteShort(short value) => Writes.Add(value);
        public void WriteInt(int value) => Writes.Add(value);
        public void WriteInteger(int value) => Writes.Add(value);
        public void WriteUInt(uint value) => Writes.Add(value);
        public void WriteUInteger(uint value) => Writes.Add(value);
        public void WriteBool(bool value) => Writes.Add(value);
        public void WriteBoolean(bool value) => Writes.Add(value);
        public void WriteString(string value) => Writes.Add(value);
        public void WriteDouble(double value) => Writes.Add(value);
    }
}
