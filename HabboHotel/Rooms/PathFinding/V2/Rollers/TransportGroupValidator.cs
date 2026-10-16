using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.PathFinding;

// Revalidates a group just before it commits: every snapshot must still hold (an earlier group's
// hooks may have moved, rotated or relocated something), and the final geometry is checked with
// plan-local occupancy that ignores only the group's own confirmed departures.
internal sealed class TransportGroupValidator(Room room, RollerTransport transport, RollerAdmission admission)
{
    internal bool IsValid(TransportGroup group)
        => group.Moves.All(Unchanged) && group.Moves.All(move => admission.Admits(move, group));

    private bool Unchanged(RollerMove move)
    {
        var snapshot = move.Snapshot;

        if (!InPlace(move.Roller, move) || move.Roller.Rotation != snapshot.RollerRotation
            || move.Roller.GetZ != snapshot.RollerZ) {
            return false;
        }

        if (move.Cargo is { } cargo) {
            return InPlace(cargo, move) && cargo.GetZ == snapshot.SourceZ
            && cargo.Rotation == snapshot.CargoRotation && CarriedZStillResolves(cargo, move);
        }

        var actor = move.Actor!;

        // A rider's source is its horse's height; the horse must still be under it.
        return ReferenceEquals(room.GetRoomUserManager().GetRoomUserByVirtualId(actor.VirtualId), actor)
            && actor.X == move.Origin.X && actor.Y == move.Origin.Y
            && (move.Horse == null ? actor.Z : move.Horse.Z) == snapshot.SourceZ
            && (move.Horse == null || ReferenceEquals(transport.Horse(actor), move.Horse)
                && move.Horse.X == move.Origin.X && move.Horse.Y == move.Origin.Y)
            && actor.Movement.LocationRevision == snapshot.ActorRevision && transport.CanRide(actor);
    }

    // The captured slide Z was resolved against the plan-time footprint; it must still be what the setter
    // would resolve now. Callers hold PlacementSync, so this is the footprint that will be committed.
    private bool CarriedZStillResolves(Item cargo, RollerMove move)
        => room.GetRoomItemHandler().ResolveFloorZ(cargo, move.Destination.X, move.Destination.Y, move.CarriedZ) == move.CarriedZ;

    private bool InPlace(Item item, RollerMove move)
        => ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)
            && item.GetX == move.Origin.X && item.GetY == move.Origin.Y;
}
