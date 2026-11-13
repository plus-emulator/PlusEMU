using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// Field order of the marketplace configuration message: enabled, commission, token batch price and size, minimum and maximum price, expiration hours,
// average price period, then sellingFeePercentage, revenueLimit and halfTaxLimit.
public class MarketplaceConfigurationComposer(IMarketplaceFeePolicy fee) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.MarketplaceConfigurationComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(true);
        packet.WriteInteger(fee.Commission);
        packet.WriteInteger(0); // Credits required to buy listing advertisements.
        packet.WriteInteger(0); // Listing advertisements granted by that purchase.
        packet.WriteInteger(fee.OfferMinimumPrice);
        packet.WriteInteger(fee.OfferMaximumPrice);
        packet.WriteInteger(fee.OfferExpirationHours);
        packet.WriteInteger(7); //Days.
        packet.WriteInteger(fee.SellingFeePercentage);
        packet.WriteInteger(fee.RevenueLimit);
        packet.WriteInteger(fee.HalfTaxLimit);
    }
}
