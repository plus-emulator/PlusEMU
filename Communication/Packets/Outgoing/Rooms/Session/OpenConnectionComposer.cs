using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <summary>class_3144 -> class_2691 (WIN63-202610131318-860371703, parse L22-26): flat id.</summary>
public sealed class OpenConnectionComposer(uint roomId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.OpenConnectionComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
    }
}
