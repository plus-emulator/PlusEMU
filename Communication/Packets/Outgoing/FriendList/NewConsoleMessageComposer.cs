using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.FriendList;

public class NewConsoleMessageComposer : IServerPacket
{
    private const int TextContent = 0;

    private readonly int _chatId;
    private readonly string _message;
    private readonly int _secondsAgo;
    private readonly int _senderId;
    private readonly string _senderName;
    private readonly string _senderFigure;

    public uint MessageId => ServerPacketHeader.NewConsoleMessageComposer;

    public NewConsoleMessageComposer(int sender, string message, int secondsAgo = 0, string senderName = "", string senderFigure = "")
    {
        _chatId = sender;
        _message = message;
        _secondsAgo = secondsAgo;
        _senderId = sender;
        _senderName = senderName;
        _senderFigure = senderFigure;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_chatId);
        packet.WriteInteger(TextContent);
        packet.WriteString(_message);
        packet.WriteInteger(_secondsAgo);
        packet.WriteString(string.Empty); // message id
        packet.WriteInteger(0); // confirmation id
        packet.WriteInteger(_senderId);
        packet.WriteString(_senderName);
        packet.WriteString(_senderFigure);
    }
}
