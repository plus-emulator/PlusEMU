using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

public class WhisperComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly string _text;
    private readonly int _emotion;
    private readonly int _colour;
    private readonly int _receiverVirtualId;

    public uint MessageId => ServerPacketHeader.WhisperComposer;

    public WhisperComposer(int virtualId, string text, int emotion, int colour, int receiverVirtualId)
    {
        _virtualId = virtualId;
        _text = text;
        _emotion = emotion;
        _colour = colour;
        _receiverVirtualId = receiverVirtualId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        RoomChatPacket.Write(packet, _virtualId, _text, _emotion, _colour, _receiverVirtualId);
    }
}
