using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <summary>
/// Room id and whether this user may manage the room's raid protection. Sent last on room entry.
/// </summary>
public sealed class RaidProtectionCapabilityComposer(uint roomId, bool canManage) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RaidProtectionCapabilityComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteBool(canManage);
    }
}
