namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class CommandIntake(MovementContext context, ForcePlacementService placement)
{
    private readonly Route _probe = new();
    public void Consume(RoomUser actor)
    {
        var state = actor.Movement;
        var command = state.Commands.Read();

        if (command == null || command.Sequence <= state.ConsumedSequence) {
            return;
        }

        state.ConsumedSequence = command.Sequence;

        if (actor.Frozen || !actor.CanWalk && command.Origin == MoveOrigin.User) {
            return;
        }

        if ((command.Flags & MoveFlags.Teleport) != 0) {
            placement.Teleport(actor, command);

            return;
        }

        if (IgnoresClick(actor, command)) {
            return;
        }

        context.Approaches.Bind(actor, command, context.ApproachGoals.Surface(command));
        state.GoalRevision++;
        state.Origin = command.Origin;
        state.Flags = command.Flags;
        state.Route.Clear();
        state.StopAfterPending = false;
        state.Cursor = 0;
        state.HasIntent = true;
        state.WaitTicks = state.StallTicks = 0;
        actor.GoalX = command.X;
        actor.GoalY = command.Y;
        state.Profile.Interaction = command.Origin == MoveOrigin.Interaction
            ? new(actor.X, actor.Y, command.X, command.Y) : null;
        actor.UnIdle();
        actor.FreezeInteracting = false;
        state.Plan = command.Origin == MoveOrigin.Interaction ? null
            : new(context.PlanningGrids.Capture(), context.Occupancy(actor));
        ResolveGoal(actor);
        context.Replan(actor);
        context.RefreshMembership(actor);
    }
    // Habbo ignores a click that has no route: a walking avatar goes on to its old goal, a standing one gets no status.
    // A click on a tile with no free spot, such as one an avatar occupies, counts as having no route.
    private bool IgnoresClick(RoomUser actor, MoveCommand command)
    {
        var state = actor.Movement;

        if (command.Origin != MoveOrigin.User || command.Approach != null
            || context.Navigation.Settings.UnreachablePolicy == "nearest") {
            return false;
        }

        var grid = context.Grid;
        var profile = context.Profiles.Refresh(actor);
        var start = new NavPosition(actor.X, actor.Y, state.SupportZ);
        var goal = GoalResolver.ResolveClick(grid, profile, start, command.X, command.Y, context.Occupancy(actor));
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity,
            profile.LegacyOverride ? grid.SlotCapacity : grid.ActiveNodeCount);

        return new PathSearch(grid, context.Navigation.Settings).Find(new(profile, start, command.X, command.Y, Goals: goal,
                View: OccupancyView.InitialPlanning), lease.Workspace, _probe)
            is PathOutcome.InvalidGoal or PathOutcome.Unreachable;
    }

    private void ResolveGoal(RoomUser actor)
    {
        var state = actor.Movement;
        var grid = context.Grid;
        var profile = context.Profiles.Refresh(actor);
        var goal = state.Origin == MoveOrigin.Interaction && grid.InBounds(actor.GoalX, actor.GoalY)
            ? new AcceptedGoal(actor.GoalX, actor.GoalY, grid.TopSlot(grid.Tile(actor.GoalX, actor.GoalY)))
            : GoalResolver.ResolveClick(grid, profile, new(actor.X, actor.Y, state.SupportZ),
                actor.GoalX, actor.GoalY, context.Occupancy(actor));
        goal = context.ApproachGoals.Narrow(actor, goal);
        state.AcceptedGoal = GoalIdentity.Capture(grid, goal);
        actor.GoalX = goal.X;
        actor.GoalY = goal.Y;
    }
}
