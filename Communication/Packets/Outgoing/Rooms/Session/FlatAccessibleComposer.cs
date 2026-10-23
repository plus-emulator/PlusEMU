using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

// The client reads the flat id, then the user name.
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
