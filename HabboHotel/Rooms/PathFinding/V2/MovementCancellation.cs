namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class MovementCancellation(MovementContext context)
{
    internal void Cancel(RoomUser actor, long discardThrough = 0)
    {
        context.Approaches.Cancel(actor);
        var state = actor.Movement;

        if (state.RollerHeldStep && state.PendingCount > 0) {
            state.StopAfterPending = true;
            state.Route.Count = Math.Min(state.Route.Count, state.Cursor + state.PendingCount);

            return;
        }

        Finish(actor, discardThrough);
    }
    // Movement ended at its goal: the approach intent, if any, is completed by the caller instead of dropped.
    internal void Finish(RoomUser actor, long discardThrough = 0)
    {
        var state = actor.Movement;
        state.GoalRevision++;
        state.AcceptedGoal = null;
        state.Plan = null;
        state.HasIntent = false;
        state.Route.Clear();
        state.Cursor = 0;
        state.PendingCount = 0;
        state.StopAfterPending = false;
        state.RollerHeldStep = false;
        state.PendingHeld = false;
        state.WaitTicks = state.StallTicks = 0;
        state.ConsumedSequence = Math.Max(state.ConsumedSequence, discardThrough);
        actor.GoalX = actor.X;
        actor.GoalY = actor.Y;
        actor.IsWalking = false;
        actor.KeepsSitWhileWalking = false;
        actor.SetStep = false;
        actor.PathRecalcNeeded = false;
        actor.RemoveStatus("mv");
        actor.UpdateNeeded = true;
        StopHorse(actor);
        context.Claims.ReleaseBatch(actor);
        context.Scheduler.Remove(actor);
        context.RefreshMembership(actor);
    }
    private void StopHorse(RoomUser actor)
    {
        if (actor.IsBot || !actor.RidingHorse) {
            return;
        }

        var horse = context.Room.GetRoomUserManager().GetRoomUserByVirtualId(actor.HorseId);

        if (horse == null) {
            return;
        }

        horse.RemoveStatus("mv");
        horse.IsWalking = false;
        horse.SetStep = false;
        horse.UpdateNeeded = true;
    }
    internal void Remove(RoomUser actor)
    {
        actor.Movement.RollerHeldStep = false;
        Cancel(actor);
        actor.Movement.State = NavState.Removing;
        actor.Movement.LocationRevision++;
        context.Claims.Remove(actor);
        context.Room.GetGameMap().RemoveUserFromMap(actor, new(actor.X, actor.Y));
    }

}
