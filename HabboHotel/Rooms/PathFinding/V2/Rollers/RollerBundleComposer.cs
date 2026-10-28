using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.Utilities;

namespace Plus.HabboHotel.Rooms.PathFinding;

// One roller slide that carries cargo and the rider standing on it.
internal sealed class RollerBundleComposer(RollerMove rider, IReadOnlyList<RollerMove> cargo) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.SlideObjectBundleComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(rider.Origin.X);
        packet.WriteInteger(rider.Origin.Y);
        packet.WriteInteger(rider.Destination.X);
        packet.WriteInteger(rider.Destination.Y);
        packet.WriteInteger(cargo.Count);

        foreach (var move in cargo) {
            packet.WriteUInteger(BuildersClubItemIds.ToWire(move.Cargo!.Id));
            packet.WriteString(TextHandling.GetString(move.SourceZ));
            packet.WriteString(TextHandling.GetString(move.CarriedZ));
        }

        packet.WriteUInteger(BuildersClubItemIds.ToWire(rider.Roller.Id));
        packet.WriteInteger(2);
        packet.WriteInteger(rider.Actor!.VirtualId);
        packet.WriteString(TextHandling.GetString(rider.SourceZ));
        packet.WriteString(TextHandling.GetString(rider.CarriedZ));
    }
}
