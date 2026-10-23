using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>canModify, then canRead, nothing after. Sent only in the room-entry burst, after YouAreController and WiredEnvironment.</summary>
public sealed class WiredPermissionsComposer(bool canModify, bool canRead) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredPermissionsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(canModify);
        packet.WriteBoolean(canRead);
    }
}
