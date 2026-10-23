using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// String claimId, int result (claim_product.result.N).
public sealed class ClaimProductResultComposer(string claimId, int result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ClaimProductResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(claimId);
        packet.WriteInteger(result);
    }
}
