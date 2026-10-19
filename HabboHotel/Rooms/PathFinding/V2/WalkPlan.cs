namespace Plus.HabboHotel.Rooms.PathFinding;

// The planning view belongs to one accepted command; physical steps always use the live grid.
internal sealed class WalkPlan(NavGrid grid, PlanningOccupancy occupancy)
{
    internal NavGrid Grid { get; } = grid;
    internal PlanningOccupancy Occupancy { get; } = occupancy;
    internal PathExclusions Exclusions { get; } = new();
    internal int RemainingSteps { get; private set; } = -1;
    internal bool Replanning { get; set; }
    internal bool PendingRecovery { get; set; }
    internal bool RetryRecovery { get; set; }
    internal void AcceptInitial(int steps) => RemainingSteps = steps;
    internal bool Fits(int steps) => RemainingSteps >= steps;
    internal void Commit(int steps) => RemainingSteps = Math.Max(0, RemainingSteps - steps);

    // Observed on AIR WIN63-202610151745-318700621: a repair after a blocked step sees new furniture next to the walker,
    // not further away, and not in the repair that follows a diagonal fallback step.
    internal void DiscoverNeighbours(NavGrid live, MovementRules liveRules, ActorProfile actor, NavPosition from)
    {
        foreach (var (dx, dy) in PathTieBreak.Neighbours) {
            var x = from.X + dx;
            var y = from.Y + dy;

            if (!live.InBounds(x, y) || !Grid.InBounds(x, y)) {
                continue;
            }

            var original = SurfaceSelection.Resting(Grid, Grid.Tile(x, y), from.Z);

            if (original < 0 || (Grid.Flags[original] & NavFlags.Transit) == 0) {
                continue;
            }

            var slot = SurfaceSelection.Resting(live, live.Tile(x, y), from.Z);

            if (slot < 0 || liveRules.CanStep(actor, from, live.Position(slot), StepPurpose.Transit, OccupancyView.Planning).Reason
                    is StepReason.NotStandable or StepReason.FloorLocked) {
                Exclusions.Block(Grid.Reference(original));
            }
        }
    }
}
