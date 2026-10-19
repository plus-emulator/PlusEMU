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
}
