namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class CommitService(Room room, RoomNavigation navigation, MovementContext context,
    MovementCancellation cancellation, RouteFallbackService fallback, ApproachCompletion approaches)
{
    private readonly MovementRules _rules = new(navigation.Grid, navigation.Settings);
    private readonly WalkSurfaceResolver _surfaces = new(context);
    private readonly LandingService _landing = new(room, navigation, context, cancellation);
    public bool Commit(RoomUser actor)
    {
        var state = actor.Movement;

        if (state.PendingCount == 0) {
            return false;
        }

        if (ReplacedSeatGoal(actor)) {
            cancellation.Cancel(actor);

            return false;
        }

        var revision = state.LocationRevision;
        var count = state.PendingCount;
        var accepted = ValidPrefix(actor);
        state.PendingCount = 0;
        actor.SetStep = false;

        if (accepted > 0) {
            LandPending(actor, accepted, count);
        }

        context.Claims.ReleaseBatch(actor);

        if (state.State != NavState.Active || state.LocationRevision != revision) {
            return accepted > 0;
        }

        if (accepted > 0) {
            state.WaitTicks = state.StallTicks = 0;
        }

        var arrived = Advance(actor, accepted, count);

        if (arrived) {
            approaches.Complete(actor, revision);
        }

        return accepted > 0;
    }
    // The Wired walk events went out with the announcement, for its last step. A batch cut short lands elsewhere: that landing raises them.
    private void LandPending(RoomUser actor, int accepted, int count)
    {
        actor.Movement.LandingInProgress = true;

        try {
            _landing.Land(actor, actor.Movement.Pending[accepted - 1], actor.Movement.PendingView, wiredOnArrival: accepted != count);
        }
        finally {
            actor.Movement.LandingInProgress = false;
        }
    }
    private bool ReplacedSeatGoal(RoomUser actor)
    {
        var state = actor.Movement;
        var target = state.Pending[state.PendingCount - 1];
        var command = state.Commands.Read();

        return context.Graph.IsSeat(target) && state.PendingPurpose[state.PendingCount - 1] == StepPurpose.Goal
            && command != null && command.Sequence > state.ConsumedSequence && !actor.Frozen
            && (actor.CanWalk || command.Origin != MoveOrigin.User);
    }
    // True when the committed prefix reached the end of the route.
    private bool Advance(RoomUser actor, int accepted, int count)
    {
        var state = actor.Movement;

        if (state.StopAfterPending) {
            cancellation.Cancel(actor);

            return false;
        }

        if (state.Plan is { PendingRecovery: true } recovery) {
            recovery.PendingRecovery = false;

            if (accepted > 0) {
                fallback.OnRouteBlocked(actor);
            }
            else {
                recovery.RetryRecovery = true;
            }

            return false;
        }

        state.Cursor += accepted;
        state.Plan?.Commit(accepted);

        if (accepted != count) {
            return false;
        }

        if (state.Cursor < state.Route.Count || !fallback.RouteFinished(state)) {
            return false;
        }

        cancellation.Finish(actor);

        return true;
    }
    private int ValidPrefix(RoomUser actor)
    {
        var state = actor.Movement;
        var profile = context.Profiles.Refresh(actor);
        var from = new NavPosition(actor.X, actor.Y, state.SupportZ);
        var accepted = 0;

        for (var i = 0; i < state.PendingCount; i++) {
            var step = state.Pending[i];

            var purpose = state.PendingPurpose[i];
            var landing = _surfaces.Resolve(actor, state.Cursor + i, from, step, state.PendingView, state.PendingZ[i], purpose);

            if (landing.Support == null && !profile.LegacyOverride && purpose != StepPurpose.Interaction) {
                break;
            }

            var to = landing.Position;

            if (!_rules.CanStep(profile, from, to, purpose, OccupancyView.Execution, context.OccupancyAt(actor, to.Slot)).Ok) {
                break;
            }

            var flags = navigation.Grid.Flags[to.Slot];
            var mask = ClaimMatrix.BlockingMask(profile, flags, purpose, OccupancyView.Execution);

            if (!context.Claims.TryClaim(actor, to.Slot, ClaimMatrix.KindFor(profile, flags, purpose), mask)) {
                break;
            }

            context.GuildGates.Accept(actor, profile, to.Slot, purpose);
            state.Pending[i] = landing.Support ?? step;
            accepted++;
            from = to;
        }

        return accepted;
    }
}
