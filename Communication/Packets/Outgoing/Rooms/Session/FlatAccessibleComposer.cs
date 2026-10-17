using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

// AIR WIN63-202610151745-318700621 package_14/class_3772.as:35 reads the flat id, then the user name.
public class FlatAccessibleComposer : IServerPacket
{
    private readonly uint _roomId;
    private readonly string _username;

    public uint MessageId => ServerPacketHeader.FlatAccessibleComposer;

    public FlatAccessibleComposer(uint roomId, string username)
    {
        _roomId = roomId;
        _username = username;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(_roomId);
        packet.WriteString(_username);
    }
}
