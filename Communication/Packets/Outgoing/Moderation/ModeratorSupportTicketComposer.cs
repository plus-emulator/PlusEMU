using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public sealed class ModeratorSupportTicketComposer(ModeratorTicketSnapshot ticket) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ModeratorSupportTicketComposer;
    public void Compose(IOutgoingPacket packet) => ModeratorTicketWire.Write(packet, ticket);
}
