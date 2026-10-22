using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public class BroadcastMessageAlertComposer : IServerPacket
{
    private readonly string _message;
    public uint MessageId => ServerPacketHeader.BroadcastMessageAlertComposer;

    public BroadcastMessageAlertComposer(string message)
    {
        _message = message;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_message);
    }
}
