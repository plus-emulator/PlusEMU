using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>A pack of mint tokens bought with silver.</summary>
public sealed record MintTokenOffer(int Id, string ProductCode, int SilverPrice, int AmountTokens);

/// <summary>AIR collectibles/class_4112.as:28-32 and class_2843.as:17-23: per offer its id, product code, silver price and tokens.</summary>
public sealed class MintTokenOffersComposer(ImmutableArray<MintTokenOffer> offers) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.MintTokenOffersComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(offers.Length);

        foreach (var offer in offers) {
            packet.WriteInteger(offer.Id);
            packet.WriteString(offer.ProductCode);
            packet.WriteInteger(offer.SilverPrice);
            packet.WriteInteger(offer.AmountTokens);
        }
    }
}
