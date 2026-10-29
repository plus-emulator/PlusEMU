using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.PathFinding;

// One roller slide that carries all cargo of a tile and the rider standing on it, if any.
internal sealed class RollerBundleComposer(RollerMove? rider, IReadOnlyList<RollerMove> cargo) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SlideObjectBundleComposer;

    public void Compose(IOutgoingPacket packet)
    {
        var lead = rider ?? cargo[0];
        packet.WriteInteger(lead.Origin.X);
        packet.WriteInteger(lead.Origin.Y);
        packet.WriteInteger(lead.Destination.X);
        packet.WriteInteger(lead.Destination.Y);
        packet.WriteInteger(cargo.Count);

        foreach (var move in cargo) {
            packet.WriteUInteger(BuildersClubItemIds.ToWire(move.Cargo!.Id));
            packet.WriteString(TextHandling.GetString(move.SourceZ));
            packet.WriteString(TextHandling.GetString(move.CarriedZ));
        }

        packet.WriteUInteger(BuildersClubItemIds.ToWire(lead.Roller.Id));

        if (rider == null) {
            return;
        }

        packet.WriteInteger(2);
        packet.WriteInteger(rider.Actor!.VirtualId);
        packet.WriteString(TextHandling.GetString(rider.SourceZ));
        packet.WriteString(TextHandling.GetString(rider.CarriedZ));
    }
}
