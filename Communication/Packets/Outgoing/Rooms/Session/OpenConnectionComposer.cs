using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <summary>Flat id.</summary>
public sealed class OpenConnectionComposer(uint roomId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.OpenConnectionComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
    }
}
