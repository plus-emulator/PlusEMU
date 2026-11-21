using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Rooms.Chat;

namespace Plus.Communication.Packets.Incoming.Moderation;

internal sealed class SubmitNewTicketEvent(IModeratorTicketService tickets) : IPacketEvent
{
    private const int MaxMessageLength = 253;
    private const int MaxChats = 100;

    // The web client sends each chat line as HTML. Its largest growth is a 3-character `[a]` table tab that becomes 79 characters.
    private const int MaxChatLineLength = RoomChatService.MaxMessageLength * 79 / 3;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var message = Cut(packet.ReadString(), MaxMessageLength);
        var category = packet.ReadInt();
        var reportedId = packet.ReadInt();
        var reportedRoomId = packet.ReadInt();
        var count = Math.Min(packet.ReadInt(), MaxChats);
        var chats = ImmutableArray.CreateBuilder<string>();

        for (var index = 0; index < count; index++) {
            packet.ReadInt();
            chats.Add(Cut(packet.ReadString(), MaxChatLineLength));
        }

        tickets.Submit(session, new(message, category, reportedId, reportedRoomId, chats.ToImmutable()));

        return Task.CompletedTask;
    }

    private static string Cut(string text, int length)
    {
        if (text.Length <= length) {
            return text;
        }

        return text[..(char.IsHighSurrogate(text[length - 1]) ? length - 1 : length)];
    }
}
