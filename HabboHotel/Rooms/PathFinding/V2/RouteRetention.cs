namespace Plus.HabboHotel.Rooms.PathFinding;

// Converts between the walk route (bound surfaces) and the retained XY suffix.
internal static class RouteRetention
{
    public static RetainedStep[] Capture(ActorMovementState state, NavGrid grid, int? start = null)
    {
        var route = state.Route;
        var cursor = start ?? state.Cursor;
        var steps = new RetainedStep[Math.Max(0, route.Count - cursor)];

        for (var i = cursor; i < route.Count; i++) {
            var surface = route.Steps[i];
            steps[i - cursor] = new(surface.Tile % grid.Width, surface.Tile / grid.Width,
                route.PurposeAt(i, state.Origin), surface.SupportItemId, route.AdvisoryZ(i));
        }

        return steps;
    }
}
