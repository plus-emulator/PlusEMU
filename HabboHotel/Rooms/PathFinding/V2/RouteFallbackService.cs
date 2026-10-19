namespace Plus.HabboHotel.Rooms.PathFinding;

// Repairs the next rejected edge of the same command; geometry farther ahead remains undiscovered.
internal sealed class RouteFallbackService(MovementContext context,
    MovementCancellation cancellation)
{
    public void OnRouteBlocked(RoomUser actor)
    {
        var state = actor.Movement;
        actor.RemoveStatus("mv");
        actor.UpdateNeeded = true;

        if (state.Plan is not { } plan) {
            cancellation.Cancel(actor);

            return;
        }

        if (plan.Replanning) {

            return;
        }

        plan.Replanning = true;
        state.GoalRevision++;
        context.Replan(actor);
    }

    public bool OwnsSearch(RoomUser actor) => actor.Movement.Plan?.Replanning == true;

    public void Reroute(RoomUser actor, Route found)
    {
        var state = actor.Movement;
        state.Plan!.Replanning = false;
        state.Route.CopyFrom(found);
        state.Route.CaptureAdvisory(state.Plan.Grid);
        state.Cursor = 0;
    }

    public bool AwaitsUnstartedSearch(RoomUser actor)
        => OwnsSearch(actor) && context.Scheduler.Contains(actor);
    public bool RouteFinished(ActorMovementState state)
        => state.Plan?.Replanning != true;
}
