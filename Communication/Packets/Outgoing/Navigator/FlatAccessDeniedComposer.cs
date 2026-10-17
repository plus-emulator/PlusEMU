using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Navigator;

// AIR WIN63-202610151745-318700621 package_60/class_2544.as:29 reads the flat id, then the user name.
public class FlatAccessDeniedComposer : IServerPacket
{
    private readonly uint _roomId;
    private readonly string _username;
    public uint MessageId => ServerPacketHeader.FlatAccessDeniedComposer;

    public FlatAccessDeniedComposer(uint roomId, string username)
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
