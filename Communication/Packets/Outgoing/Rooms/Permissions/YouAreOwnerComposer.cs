using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Permissions;

/// <summary>Flat id.</summary>
public sealed class YouAreOwnerComposer(uint roomId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.YouAreOwnerComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
    }
}
