using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.Packets.Outgoing.Moderation;

public sealed class ModeratorInitComposer(ModeratorInitSnapshot data) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ModeratorInitComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(data.Tickets.Length);

        foreach (var ticket in data.Tickets) {
            ModeratorTicketWire.Write(packet, ticket);
        }

        packet.WriteInteger(data.UserPresets.Length);

        foreach (var preset in data.UserPresets) {
            packet.WriteString(preset);
        }

        packet.WriteInteger(0); // AIR reads and drops this string list

        for (var index = 0; index < 7; index++) {
            packet.WriteBoolean(true);
        }

        packet.WriteInteger(data.RoomPresets.Length);

        foreach (var preset in data.RoomPresets) {
            packet.WriteString(preset);
        }
    }
}
