using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// String claimId, boolean hasClaimed.
public sealed class HasClaimedProductResponseComposer(string claimId, bool hasClaimed) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.HasClaimedProductResponseComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(claimId);
        packet.WriteBoolean(hasClaimed);
    }
}
