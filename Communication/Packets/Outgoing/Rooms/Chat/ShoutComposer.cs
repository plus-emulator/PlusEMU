using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

public class ShoutComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly string _message;
    private readonly int _emotion;
    private readonly int _colour;
    private readonly int _receiverVirtualId;

    public uint MessageId => ServerPacketHeader.ShoutComposer;

    public ShoutComposer(int virtualId, string message, int emotion, int colour, int receiverVirtualId)
    {
        _virtualId = virtualId;
        _message = message;
        _emotion = emotion;
        _colour = colour;
        _receiverVirtualId = receiverVirtualId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        RoomChatPacket.Write(packet, _virtualId, _message, _emotion, _colour, _receiverVirtualId);
    }
}
