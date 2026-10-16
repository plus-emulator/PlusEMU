using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Permissions;

/// <summary>class_2670 -> class_4114 (WIN63-202610131318-860371703, parse L27-30): flat id.</summary>
public sealed class YouAreOwnerComposer(uint roomId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.YouAreOwnerComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
    }
}
