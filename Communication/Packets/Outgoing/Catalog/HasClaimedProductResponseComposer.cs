using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// AIR WIN63-202610151745-318700621, parser package_174/class_2885.as:36: string claimId, boolean hasClaimed.
public sealed class HasClaimedProductResponseComposer(string claimId, bool hasClaimed) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.HasClaimedProductResponseComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(claimId);
        packet.WriteBoolean(hasClaimed);
    }
}
