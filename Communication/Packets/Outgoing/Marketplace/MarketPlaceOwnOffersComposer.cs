using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Catalog.Marketplace;

namespace Plus.Communication.Packets.Outgoing.Marketplace;

public class MarketPlaceOwnOffersComposer : IServerPacket
{
    private readonly MarketplaceOwnOffers _data;
    public uint MessageId => ServerPacketHeader.MarketPlaceOwnOffersComposer;

    public MarketPlaceOwnOffersComposer(MarketplaceOwnOffers data) =>
        _data = data with { Offers = data.Offers.ToArray() };

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_data.AccumulatedAmount);
        packet.WriteInteger(_data.Offers.Count);

        foreach (var offer in _data.Offers) {
            packet.WriteInteger(offer.OfferId);
            packet.WriteInteger(offer.State);
            packet.WriteInteger(offer.LimitedNumber > 0 ? 3 : offer.ItemType);
            packet.WriteInteger(offer.SpriteId);

            if (offer.LimitedNumber > 0) {
                packet.WriteInteger(offer.LimitedNumber);
                packet.WriteInteger(offer.LimitedStack);
            }
            else {
                if (offer.ItemType == 1) {
                    packet.WriteInteger(0);
                }

                packet.WriteString(offer.ExtraData);
            }

            packet.WriteInteger(offer.TotalPrice);
            packet.WriteInteger(offer.MinutesRemaining);
            packet.WriteInteger(offer.AveragePrice);

            if (offer.State is 2 or 3) {
                packet.WriteInteger(unchecked((int)(offer.StatusTime >> 32)));
                packet.WriteInteger(unchecked((int)offer.StatusTime));
            }
        }
    }
}
