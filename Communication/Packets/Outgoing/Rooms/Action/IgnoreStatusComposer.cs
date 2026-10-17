using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Action;

/// <summary>AIR WIN63-202610151745-318700621 package_37/class_4292.as:25: result, then the ignored user's id.</summary>
public class IgnoreStatusComposer(IgnoreStatus status, int userId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.IgnoreStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger((int)status);
        packet.WriteInteger(userId);
    }
}
