using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>
/// An offer of the Shop tab. MintLimit -1 means no limit; MintedCount counts every collectible of the product. MinLevel is the collector
/// level a wallet needs to buy it, 0 for none.
/// </summary>
public sealed record NftStoreOffer(string ProductCode, int EmeraldPrice, bool IsFeatured, bool IsLimited, int MintLimit, long MintedCount, int MinLevel);

/// <summary>
/// AIR collectibles/class_3977.as:27-31 and class_3825.as:23-33: per offer its code, price, flags, limit, minted count and product.
/// The min level before the product is this hotel's own field, so the official AIR client cannot read this packet.
/// </summary>
public sealed class NftStoreOffersComposer(ImmutableArray<(NftStoreOffer Offer, CollectibleProduct Product)> offers) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NftStoreOffersComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(offers.Length);

        foreach (var (offer, product) in offers) {
            packet.WriteString(offer.ProductCode);
            packet.WriteInteger(offer.EmeraldPrice);
            packet.WriteBoolean(offer.IsFeatured);
            packet.WriteBoolean(offer.IsLimited);
            packet.WriteInteger(offer.MintLimit);
            packet.WriteInteger((int)offer.MintedCount);
            packet.WriteInteger(offer.MinLevel);
            NftProductWriter.Write(packet, product);
        }
    }
}
