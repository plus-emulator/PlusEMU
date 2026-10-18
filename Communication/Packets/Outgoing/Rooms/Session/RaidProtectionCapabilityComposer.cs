using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <summary>
/// package_236/class_4123 (WIN63-202610151745-318700621, parse L18): room id and whether this user may manage the room's raid protection
/// (RaidProtectionSettingsController.as:183). Sent last on room entry.
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
