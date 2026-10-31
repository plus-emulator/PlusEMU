namespace Plus.HabboHotel.Rooms.PathFinding;

// Runs between compilation and Version publication. No arrival or game hooks run here.
internal sealed class GeometryPublicationService(MovementContext context, RebindService rebind)
{
    private NavGrid Grid => context.Grid;

    internal void BeforePublish(IReadOnlySet<int> tiles)
    {
        context.Claims.RemapSlots(Grid.ReleasedSlots, LiveSlot);

        foreach (var actor in context.Room.GetRoomUserManager().GetUserList()) {
            var state = actor.Movement;

            if (state.State != NavState.Active) {
                continue;
            }

            var current = Grid.InBounds(actor.X, actor.Y) && tiles.Contains(Grid.Tile(actor.X, actor.Y));
            var pending = PendingTouches(state, tiles);

            if (current) {
                rebind.BeforePublish(actor);
            }

            RetargetPending(actor, tiles);

            if (current || pending) {
                context.RefreshMembership(actor);
            }

        }
    }

    private static bool PendingTouches(ActorMovementState state, IReadOnlySet<int> tiles)
    {
        for (var index = 0; index < state.PendingCount; index++) {
            if (tiles.Contains(state.Pending[index].Tile)) {
                return true;
            }
        }

        return false;
    }

    private void RetargetPending(RoomUser actor, IReadOnlySet<int> tiles)
    {
        var state = actor.Movement;

        for (var index = 0; index < state.PendingCount; index++) {
            var target = state.Pending[index];
            var privileged = state.Profile.LegacyOverride || state.Origin == MoveOrigin.Interaction;
            var slot = context.Graph.Slot(target, state.PendingView);

            if (!tiles.Contains(target.Tile) || slot >= 0 && (Grid.Active(slot) || privileged) || Retarget(actor, index)) {
                continue;
            }

            context.Claims.ReleaseBatch(actor);
            // Furniture that Wired puts on the target of an announced step does not stop the step. Any other change releases the batch for commit's prefix validation.
            state.PendingHeld = context.Room.GetWired()?.ExecutingFiring != null;

            return;
        }

    }

    // The announced surface was rebuilt away (furniture moved or changed height) but its tile may still stand:
    // the step keeps its claim on the surface it would land on, and the commit re-validates it as usual.
    private bool Retarget(RoomUser actor, int index)
    {
        var state = actor.Movement;
        var target = state.Pending[index];
        var landing = context.Graph.ResolveLanding(target, state.PendingView, state.PendingZ[index]);

        if (landing.Support is not { } replacement) {
            return false;
        }

        var slot = landing.Position.Slot;
        var purpose = state.PendingPurpose[index];
        var mask = ClaimMatrix.BlockingMask(state.Profile, Grid.Flags[slot], purpose, OccupancyView.Execution);

        if (!context.Claims.TryClaim(actor, slot, ClaimMatrix.KindFor(state.Profile, Grid.Flags[slot], purpose), mask)) {
            return false;
        }

        state.Pending[index] = replacement;

        return true;
    }

    private int LiveSlot(SurfaceRef surface)
    {
        var slot = Grid.SlotOf(surface);

        return slot >= 0 && Grid.Active(slot) ? slot : -1;
    }

}
