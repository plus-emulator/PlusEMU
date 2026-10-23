using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.Packets.Outgoing.Moderation;

// The issue entry shared by the moderator init and the issue info messages.
internal static class ModeratorTicketWire
{
    public static void Write(IOutgoingPacket packet, ModeratorTicketSnapshot ticket)
    {
        packet.WriteInteger(ticket.Id);
        packet.WriteInteger((int)ticket.Status);
        packet.WriteInteger(ticket.Type);
        packet.WriteInteger(ticket.Category);
        packet.WriteInteger(ticket.AgeMilliseconds);
        packet.WriteInteger(ticket.Priority);
        packet.WriteInteger(0); // grouping id
        packet.WriteInteger(ticket.SenderId);
        packet.WriteString(ticket.SenderName);
        packet.WriteInteger(ticket.ReportedId);
        packet.WriteString(ticket.ReportedName);
        packet.WriteInteger(ticket.ModeratorId);
        packet.WriteString(ticket.ModeratorName);
        packet.WriteString(ticket.Issue);
        packet.WriteUInteger(ticket.RoomId); // chat record id
        packet.WriteInteger(0); // chat patterns
    }
}
