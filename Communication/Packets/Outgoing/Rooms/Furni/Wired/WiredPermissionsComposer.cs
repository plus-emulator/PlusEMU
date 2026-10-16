using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// package_121.class_3679 -> class_2713 (WIN63-202610131318-860371703, parse L25-30): canModify, then canRead, nothing after.
/// Hotel captures show it only in the room-entry burst, after YouAreController and WiredEnvironment.
/// </summary>
public sealed class WiredPermissionsComposer(bool canModify, bool canRead) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredPermissionsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(canModify);
        packet.WriteBoolean(canRead);
    }
}
