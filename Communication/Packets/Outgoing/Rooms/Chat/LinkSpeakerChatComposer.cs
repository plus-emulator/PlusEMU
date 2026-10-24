using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

/// <summary>A Link Speaker bubble: chat from no user (index -1), bubble style 253, no name, shown at the add-on's tile.</summary>
public sealed record LinkSpeakerChatComposer(int ReceiverVirtualId, string Message, int X, int Y) : IServerPacket
{
    private const int NoUser = -1;
    private const int BubbleStyle = 253;

    public uint MessageId => ServerPacketHeader.ChatComposer;

    public void Compose(IOutgoingPacket packet) =>
        RoomChatPacket.Write(packet, NoUser, Message, 0, BubbleStyle, ReceiverVirtualId, hideUsername: true, location: (X, Y));
}
