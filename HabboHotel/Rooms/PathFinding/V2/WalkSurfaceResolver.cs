namespace Plus.HabboHotel.Rooms.PathFinding;

// Rebind layers along the retained XY route when its next surface changes or cannot be reached. This never
// changes the route's destination, length, or planning view, and never truncates a future edge.
internal sealed class WalkSurfaceResolver(MovementContext context)
{
    private readonly ValidPrefixFinder _prefix = new();
    private readonly MovementRules _rules = new(context.Grid, context.Navigation.Settings);

    internal LandingSurface Resolve(RoomUser actor, int index, NavPosition from, SurfaceRef step,
        GraphView view, double plannedZ, StepPurpose purpose)
    {
        var grid = context.Grid;
        var landing = context.Graph.ResolveLanding(step, view, plannedZ);
        var slot = grid.SlotOf(step);

        if (!grid.Layered || view != GraphView.Surface || purpose == StepPurpose.Interaction
            || actor.Movement.Plan?.PendingRecovery == true) {
            return landing;
        }

        var profile = context.Profiles.Refresh(actor);

        if (slot >= 0 && grid.Active(slot) && Math.Abs(grid.WalkZ[slot] - plannedZ) <= SurfaceSelection.Tolerance
            && (grid.SurfaceCount(step.Tile) < 2 || _rules.CanStep(profile, from, landing.Position,
                purpose, OccupancyView.Execution, context.OccupancyAt(actor, landing.Position.Slot)).Ok)) {
            return landing;
        }

        var occupancy = context.Occupancy(actor);

        for (var i = 0; i < occupancy.Targets.Length; i++) {
            occupancy.Targets[i] &= NavPrefixGraph.Kept;
        }

        var graph = new NavPrefixGraph(grid, _rules, profile, occupancy);
        var prefix = _prefix.Find(new(from.X, from.Y, from.Z, 0, from.Slot),
            RouteRetention.Capture(actor.Movement, grid, index), graph);

        return prefix.Length == 0 ? landing
            : new(grid.Position(prefix[0].Slot), grid.Reference(prefix[0].Slot));
    }
}
