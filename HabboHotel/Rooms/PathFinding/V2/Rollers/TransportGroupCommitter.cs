using Plus.Communication.Packets;

namespace Plus.HabboHotel.Rooms.PathFinding;

// Simultaneous group contract (§14.9): validate (including every furniture-setter rejection), reserve,
// commit positions and furniture without callbacks, publish once, settle deferred furniture effects,
// then emit the captured slides and dispatch hooks in roller-id order. Any failure is a whole-group no-op.
internal sealed class TransportGroupCommitter(Room room, RollerTransport transport, TransportGroupValidator validator)
{
    internal bool TryCommit(TransportGroup group)
    {
        var ordered = group.Moves.OrderBy(move => move.Roller.Id).ToList();
        var actors = ordered.Where(move => move.Actor != null).ToList();
        var furniture = ordered.Where(move => move.Cargo != null)
            .Select(move => new FloorMove(move.Cargo!, move.Destination.X, move.Destination.Y, move.CarriedZ)).ToList();
        var items = room.GetRoomItemHandler();
        transport.RefreshCapabilities(actors.Select(move => move.Actor!));

        // Final validation and the positional commit see one placement state; no concurrent move or
        // rotation can land between them.
        lock (room.GetGameMap().PlacementSync) {
            if (!validator.IsValid(group) || !transport.Reserve(group)) {
                return false;
            }

            items.CommitFloorMoves(furniture);
            transport.CommitActors(actors);
            transport.HoldDepartingSteps(ordered);
        }

        transport.Publish(actors);
        items.SettleFloorMoves(furniture);
        room.SendPacket(Slides(ordered));

        foreach (var move in actors.Where(StillLanded)) {
            transport.Land(move);
        }

        return true;
    }

    // A rider standing on cargo slides in the same bundle as the cargo it stands on, and all cargo of one roller slide together.
    private static List<IServerPacket> Slides(List<RollerMove> ordered)
    {
        var bundles = new Dictionary<RollerMove, RollerBundleComposer>();
        var carried = new HashSet<RollerMove>();

        foreach (var rider in ordered.Where(move => move.Actor != null && move.Horse == null)) {
            var cargo = ordered.Where(move => move.Cargo != null && move.Roller == rider.Roller && move.Origin == rider.Origin).ToList();

            if (cargo.Count > 0) {
                bundles[rider] = new(rider, cargo);
                carried.UnionWith(cargo);
            }
        }

        foreach (var group in ordered.Where(move => move.Cargo != null && !carried.Contains(move)).GroupBy(move => move.Roller).Where(group => group.Count() > 1)) {
            var cargo = group.OrderBy(move => move.SourceZ).ToList();
            bundles[cargo[0]] = new(null, cargo);
            carried.UnionWith(cargo.Skip(1));
        }

        var packets = new List<IServerPacket>();

        foreach (var move in ordered.Where(move => !carried.Contains(move))) {
            if (bundles.TryGetValue(move, out var bundle)) {
                packets.Add(bundle);
            }
            else {
                packets.AddRange(move.HorseSlide == null ? [move.Slide] : [move.HorseSlide, move.Slide]);
            }
        }

        return packets;
    }

    // An earlier hook in this group may already have moved the actor elsewhere.
    private static bool StillLanded(RollerMove move)
        => move.Actor!.X == move.Destination.X && move.Actor.Y == move.Destination.Y;
}
