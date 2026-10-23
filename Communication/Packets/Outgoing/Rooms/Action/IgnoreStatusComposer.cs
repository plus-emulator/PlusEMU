using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Action;

/// <summary>Result, then the ignored user's id.</summary>
public class IgnoreStatusComposer(IgnoreStatus status, int userId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.IgnoreStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)status);
        packet.WriteInteger(userId);
    }
}
