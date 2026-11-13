using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Catalog.Marketplace;

namespace Plus.Communication.Packets.Outgoing.Marketplace;

public class MarketplaceItemStatsComposer : IServerPacket
{
    private readonly int _itemId;
    private readonly uint _spriteId;
    private readonly MarketplaceItemStats _stats;
    public uint MessageId => ServerPacketHeader.MarketplaceItemStatsComposer;

    public MarketplaceItemStatsComposer(int itemId, uint spriteId, int averagePrice, int offerCount)
        : this(itemId, spriteId, new MarketplaceItemStats(averagePrice, offerCount)) { }

    public MarketplaceItemStatsComposer(int itemId, uint spriteId, MarketplaceItemStats stats)
    {
        _itemId = itemId;
        _spriteId = spriteId;
        _stats = stats;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_stats.AveragePrice);
        packet.WriteInteger(_stats.OfferCount);
        packet.WriteInteger(_stats.History == null ? 0 : 30);
        var history = _stats.History ?? [];
        packet.WriteInteger(history.Count);

        foreach (var day in history) {
            packet.WriteInteger(day.DayOffset);
            packet.WriteInteger(day.AveragePrice);
            packet.WriteInteger(day.SoldAmount);
        }

        packet.WriteInteger(_itemId);
        packet.WriteUInteger(_spriteId);
        packet.WriteInteger(_stats.LowestPrice);
        packet.WriteInteger(_stats.SuggestedPrice);
    }
}
