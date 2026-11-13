using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Marketplace;

public class MarketPlaceOffersComposer : IServerPacket
{
    private readonly MarketplaceOffersSnapshot _offers;
    public uint MessageId => ServerPacketHeader.MarketPlaceOffersComposer;

    public MarketPlaceOffersComposer(MarketplaceOffersSnapshot offers)
    {
        _offers = offers;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_offers.Offers.Length);

        foreach (var offer in _offers.Offers) {
            packet.WriteUInteger(offer.OfferId);
            packet.WriteInteger(1); //State
            packet.WriteInteger(offer.LimitedNumber > 0 ? 3 : offer.ItemType);
            packet.WriteUInteger(offer.SpriteId);

            if (offer.LimitedNumber > 0) {
                packet.WriteUInteger(offer.LimitedNumber);
                packet.WriteUInteger(offer.LimitedStack);
            }
            else {
                if (offer.ItemType == 1) {
                    packet.WriteInteger(0);
                }

                packet.WriteString(offer.ExtraData);
            }

            packet.WriteInteger(offer.TotalPrice);
            packet.WriteInteger(0);
            packet.WriteInteger(offer.AveragePrice);
            packet.WriteInteger(offer.Count);
        }

        packet.WriteInteger(_offers.TotalFound ?? _offers.Offers.Length);
    }
}
