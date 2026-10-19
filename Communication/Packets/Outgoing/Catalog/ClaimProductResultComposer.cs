using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// AIR WIN63-202610151745-318700621, parser package_174/class_3882.as:36: string claimId, int result (claim_product.result.N).
public sealed class ClaimProductResultComposer(string claimId, int result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ClaimProductResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(claimId);
        packet.WriteInteger(result);
    }
}
