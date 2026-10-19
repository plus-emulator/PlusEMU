namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class MovementSearch(Room room, RoomNavigation navigation, MovementContext context,
    MovementCancellation cancellation, RouteFallbackService fallback, ApproachCompletion approaches)
{
    private SearchScheduler<RoomUser> _scheduler => context.Scheduler;
    // Keep a replacement separate until it is known to fit the remaining allowance.
    private readonly Route _fallbackRoute = new();
    public void Remove(RoomUser actor) => _scheduler.Remove(actor);
    public void Enqueue(RoomUser actor)
    {
        var state = actor.Movement;

        if (!state.HasIntent) {
            return;
        }

        _scheduler.Enqueue(actor, state.LifetimeId, state.GoalRevision);
    }
    public void Run() => _scheduler.Run(navigation.Settings.MaxExpansionsPerRoomTick, Current, Search, Complete);
    // A bump is repaired in the same walking tick. Every retry discovers a new blocked edge,
    // and the replacement must fit the command's remaining route allowance.
    internal void Recover(RoomUser actor)
    {
        if (!fallback.OwnsSearch(actor)) {
            return;
        }

        _scheduler.Remove(actor);
        var job = new SearchJob<RoomUser>(actor, actor.Movement.LifetimeId, actor.Movement.GoalRevision);
        var result = Search(job);

        if (Current(job)) {
            Complete(job, result);
        }
    }
    private bool Current(SearchJob<RoomUser> job)
    {
        var state = job.Actor.Movement;

        return state.State == NavState.Active && state.LifetimeId == job.LifetimeId && state.GoalRevision == job.GoalRevision && state.HasIntent;
    }
    private SearchResult Search(SearchJob<RoomUser> job)
    {
        var actor = job.Actor;
        var state = actor.Movement;
        var complete = fallback.OwnsSearch(actor);
        var into = complete ? _fallbackRoute : state.Route;
        var profile = context.Profiles.Refresh(actor);

        if (!profile.LegacyOverride && state.Plan is { RemainingSteps: < 0 }
            && state.AcceptedGoal is { } accepted && accepted.Resolve(navigation.Grid) == null) {
            return new(PathOutcome.InvalidGoal, 0);
        }

        var grid = state.Plan?.Grid ?? navigation.Grid;
        var occupancy = complete ? context.Occupancy(actor, grid)
            : state.Plan?.Occupancy ?? context.Occupancy(actor);
        var start = new NavPosition(actor.X, actor.Y, state.SupportZ);
        var goal = state.AcceptedGoal?.Resolve(grid) ?? context.ApproachGoals.Reresolve(actor,
            InteractionGoal(actor, start) ?? GoalResolver.ResolveClick(grid, profile, start, actor.GoalX, actor.GoalY, occupancy));
        actor.GoalX = goal.X;
        actor.GoalY = goal.Y;
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity,
            profile.LegacyOverride ? grid.SlotCapacity : grid.ActiveNodeCount);
        var outcome = state.Origin == MoveOrigin.Interaction && goal.Slot >= 0
            ? InteractionRoute(actor, start, goal, into)
            : new PathSearch(grid, navigation.Settings).Find(new(profile, start, goal.X, goal.Y, occupancy, goal, complete,
                state.Plan?.Exclusions, complete ? OccupancyView.Planning : OccupancyView.InitialPlanning), lease.Workspace, into);

        if (complete) {
            return new(outcome, lease.Workspace.Expansions);
        }

        return ResolveNearest(actor, start, profile, occupancy, lease.Workspace, outcome);
    }
    private SearchResult ResolveNearest(RoomUser actor, NavPosition start, ActorProfile profile,
        PlanningOccupancy occupancy, PathWorkspace workspace, PathOutcome outcome)
    {
        var state = actor.Movement;
        var expansions = workspace.Expansions;

        if (navigation.Settings.UnreachablePolicy == "nearest" && outcome is PathOutcome.InvalidGoal or PathOutcome.Unreachable) {
            outcome = new NearestGoalSearch(state.Plan?.Grid ?? navigation.Grid, navigation.Settings)
                .Find(new(profile, start, actor.GoalX, actor.GoalY, occupancy, View: OccupancyView.InitialPlanning), workspace, state.Route);
            expansions += workspace.Expansions;

            if (state.Route.GoalSurface is { } target) {
                actor.GoalX = target.Tile % navigation.Grid.Width;
                actor.GoalY = target.Tile / navigation.Grid.Width;
                var grid = state.Plan?.Grid ?? navigation.Grid;
                var slot = grid.SlotOf(target);
                state.AcceptedGoal = GoalIdentity.Capture(grid, new(actor.GoalX, actor.GoalY, slot));
            }
        }

        return new(outcome, expansions);
    }
    private AcceptedGoal? InteractionGoal(RoomUser actor, NavPosition start)
    {
        if (actor.Movement.Origin != MoveOrigin.Interaction || !navigation.Grid.InBounds(actor.GoalX, actor.GoalY)) {
            return null;
        }

        return new(actor.GoalX, actor.GoalY, navigation.Grid.TopSlot(navigation.Grid.Tile(actor.GoalX, actor.GoalY)));
    }
    private PathOutcome InteractionRoute(RoomUser actor, NavPosition start, AcceptedGoal goal, Route route)
    {
        var target = navigation.Grid.Position(goal.Slot);

        if (!new MovementRules(navigation.Grid, navigation.Settings).CanStep(actor.Movement.Profile,
            start, target, StepPurpose.Interaction, OccupancyView.Execution).Ok) {
            return PathOutcome.InvalidGoal;
        }

        route.Count = 1;
        route.GridVersion = navigation.Grid.Version;
        route.View = GraphView.Surface;
        route.Set(0, navigation.Grid.Reference(goal.Slot));
        route.GoalSurface = navigation.Grid.Reference(goal.Slot);

        return PathOutcome.Found;
    }
    private void Complete(SearchJob<RoomUser> job, SearchResult result)
    {
        var actor = job.Actor;

        if (result.Outcome == PathOutcome.Found) {
            Install(actor);

            return;
        }

        if (result.Outcome == PathOutcome.AlreadyThere) {
            Arrive(actor);

            return;
        }

        cancellation.Cancel(actor);
    }
    private void Arrive(RoomUser actor)
    {
        cancellation.Finish(actor);
        PostureService.Apply(room, navigation.Grid, actor);
        approaches.Complete(actor, actor.Movement.LocationRevision);
    }
    private void Install(RoomUser actor)
    {
        if (fallback.OwnsSearch(actor)) {
            if (actor.Movement.Plan is { } plan && !plan.Fits(_fallbackRoute.Count)) {
                cancellation.Cancel(actor);

                return;
            }

            fallback.Reroute(actor, _fallbackRoute);
        }
        else {
            actor.Movement.Route.CaptureAdvisory(actor.Movement.Plan?.Grid ?? navigation.Grid);
            actor.Movement.Plan?.AcceptInitial(actor.Movement.Route.Count);
        }

        actor.IsWalking = true;
    }
}
