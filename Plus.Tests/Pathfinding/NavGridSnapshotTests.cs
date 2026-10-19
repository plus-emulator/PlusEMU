using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests.Pathfinding;

public class NavGridSnapshotTests
{
    [Fact]
    public void RemovedOriginalBlockerRemainsBlockedUntilANewSnapshot()
    {
        var (grid, inputs, compiler) = NavTest.Create(3, 1);
        inputs.Publish(NavTest.Record(10, 1, [1], h: 1, walkable: false));
        compiler.ApplyNow();
        var snapshot = grid.CreateSnapshot();
        inputs.Publish(NavTest.Record(10, 2, [1], h: 1, walkable: false, removed: true));
        compiler.ApplyNow();
        Assert.Equal(PathOutcome.Unreachable, Find(snapshot, new(new(), new(0, 0, 0), 2, 0), new()));
        var route = new Route();
        Assert.Equal(PathOutcome.Found, Find(grid.CreateSnapshot(), new(new(), new(0, 0, 0), 2, 0), route));
        Assert.Equal(new[] { 1, 2 }, route.Steps.ToArray().Select(step => step.Tile));
    }

    [Fact]
    public void NewBlockerDoesNotRemoveAnUndiscoveredSurfaceFromTheSnapshot()
    {
        var (grid, inputs, compiler) = NavTest.Create(3, 1);
        var snapshot = grid.CreateSnapshot();
        inputs.Publish(NavTest.Record(10, 1, [1], h: 1, walkable: false));
        compiler.ApplyNow();
        Assert.Equal(PathOutcome.Unreachable, Find(grid, new(new(), new(0, 0, 0), 2, 0), new()));
        var route = new Route();
        Assert.Equal(PathOutcome.Found, Find(snapshot, new(new(), new(0, 0, 0), 2, 0), route));
        Assert.Equal(new[] { 1, 2 }, route.Steps.ToArray().Select(step => step.Tile));
        var exclusions = new PathExclusions();
        exclusions.Block(new(1, 0, SurfaceKind.Floor));
        Assert.Equal(PathOutcome.Unreachable, Find(snapshot, new(new(), new(0, 0, 0), 2, 0, Exclusions: exclusions), new()));
    }

    [Fact]
    public void CommandsAtTheSamePublishedVersionShareGeometryButLaterCommandsSeeChanges()
    {
        var (grid, inputs, compiler) = NavTest.Create(3, 1);
        var cache = new PlanningGridCache(grid);
        var first = cache.Capture();
        Assert.Same(first, cache.Capture());
        inputs.Publish(NavTest.Record(10, 1, [1], h: 1, walkable: false));
        compiler.ApplyNow();
        var second = cache.Capture();
        Assert.NotSame(first, second);
        Assert.Same(second, cache.Capture());
        Assert.Equal(PathOutcome.Found, Find(first, new(new(), new(0, 0, 0), 2, 0), new()));
        Assert.Equal(PathOutcome.Unreachable, Find(second, new(new(), new(0, 0, 0), 2, 0), new()));
    }

    [Fact]
    public void LayeredSnapshotKeepsItsSupportHeightAndAddressWhenLiveSlotsAreReused()
    {
        var (grid, inputs, compiler) = NavTest.Create(4, 1, new() { LayeringEnabled = true });
        inputs.Publish(NavTest.Record(10, 1, [1], z: 2));
        compiler.ApplyNow();
        var deck = new SurfaceRef(1, 10, SurfaceKind.Top);
        var snapshot = grid.CreateSnapshot();
        var originalSlot = snapshot.SlotOf(deck);
        Assert.True(originalSlot >= snapshot.TileCount);
        inputs.Publish(NavTest.Record(10, 2, [2], z: 3));
        compiler.ApplyNow();
        Assert.Equal(originalSlot, grid.SlotOf(new(2, 10, SurfaceKind.Top)));
        Assert.Equal(-1, grid.SlotOf(deck));
        Assert.Equal(originalSlot, snapshot.SlotOf(deck));
        Assert.Equal(new NavPosition(1, 0, 2, originalSlot), snapshot.Position(originalSlot));
        Assert.Equal(new uint[] { 10 }, snapshot.Contacts(originalSlot).ToArray());
        Assert.True(snapshot.Connectivity.SameComponent(0, originalSlot));
        Assert.Equal(PathOutcome.AlreadyThere, Find(snapshot,
            new(new(), new(1, 0, 2), 1, 0, Goals: new(1, 0, originalSlot)), new()));
    }

    [Fact]
    public void PublishedFloorLocksAndBedPillowsDoNotChangeOlderSnapshotGoals()
    {
        var (grid, inputs, compiler) = NavTest.Create(4, 2, new() { LayeringEnabled = true });
        inputs.Publish(NavTest.Record(10, 1, [1, 5], h: 1, interaction: InteractionType.Bed));
        compiler.ApplyNow();
        var snapshot = grid.CreateSnapshot();
        var bed = snapshot.SlotOf(new(1, 10, SurfaceKind.BedBase));
        Assert.True(bed >= 0);
        grid.PillowTiles[grid.SlotOf(new(1, 10, SurfaceKind.BedBase))][0] = 7;
        grid.FloorLocks[1] = 1;
        grid.Flags[1] |= NavFlags.FloorLocked;
        Assert.Equal(0, snapshot.FloorLocks[1]);
        Assert.Equal(new[] { 1 }, snapshot.PillowTiles[bed]);
        var goal = GoalResolver.ResolveClick(snapshot, new(), new(0, 0, 0), 1, 1, null);
        Assert.Equal((1, 0), (goal.X, goal.Y));
        Assert.True(goal.Contains(bed));
    }

    [Fact]
    public void ABlockedFloorDoesNotExcludeTheBridgeOnTheSameTile()
    {
        var (grid, inputs, compiler) = NavTest.Create(3, 1, new() { LayeringEnabled = true });
        inputs.Publish(NavTest.Record(10, 1, [1], z: 1));
        compiler.ApplyNow();
        var snapshot = grid.CreateSnapshot();
        var exclusions = new PathExclusions();
        exclusions.Block(new(1, 0, SurfaceKind.Floor));
        var route = new Route();
        Assert.Equal(PathOutcome.Found, Find(snapshot, new(new(), new(0, 0, 0), 2, 0, Exclusions: exclusions), route));
        Assert.Equal(new SurfaceRef(1, 10, SurfaceKind.Top), route.Steps[0]);
        Assert.Equal(new SurfaceRef(2, 0, SurfaceKind.Floor), route.Steps[1]);
    }

    [Fact]
    public void DirectedFailureDoesNotExcludeAnotherApproachOrTheReverseEdge()
    {
        var (grid, _, _) = NavTest.Create(2, 2);
        var exclusions = new PathExclusions();
        exclusions.Block(new(0, 0, 0, 99), new(1, 0, SurfaceKind.Floor));
        var profile = new ActorProfile { DiagonalEnabled = false };
        var route = new Route();
        Assert.Equal(PathOutcome.Found, Find(grid, new(profile, new(0, 0, 0), 1, 0, Exclusions: exclusions), route));
        Assert.Equal(new[] { 2, 3, 1 }, route.Steps.ToArray().Select(step => step.Tile));
        Assert.Equal(PathOutcome.Found, Find(grid, new(profile, new(1, 0, 0), 0, 0, Exclusions: exclusions), route));
        Assert.Equal(new[] { 0 }, route.Steps.ToArray().Select(step => step.Tile));
    }

    [Fact]
    public void ADiscoveredBlockedGoalCannotBeReturnedAsAlreadyThere()
    {
        var (grid, _, _) = NavTest.Create(2, 1);
        var exclusions = new PathExclusions();
        exclusions.Block(new(1, 0, SurfaceKind.Floor));
        Assert.Equal(PathOutcome.InvalidGoal, Find(grid, new(new(), new(1, 0, 0), 1, 0, Exclusions: exclusions), new()));
    }

    [Fact]
    public void RepeatedDiscoveriesAreIdempotentAndIgnoreReusedSourceSlots()
    {
        var exclusions = new PathExclusions();
        var target = new SurfaceRef(1, 0, SurfaceKind.Floor);
        Assert.True(exclusions.Block(new(0, 0, 0, 10), target));
        Assert.False(exclusions.Block(new(0, 0, 0, 20), target));
        Assert.False(exclusions.Allows(new(0, 0, 0, 30), target));
        Assert.True(exclusions.Allows(new(0, 0, 1, 30), target));
        Assert.Equal(1, exclusions.Count);
        Assert.True(exclusions.Block(target));
        Assert.False(exclusions.Block(target));
        Assert.Equal(2, exclusions.Count);
        Assert.False(exclusions.Allows(new(0, 0, 1, 30), target));
    }

    private static PathOutcome Find(NavGrid grid, SearchRequest request, Route route)
    {
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);

        return new PathSearch(grid, new()).Find(request, lease.Workspace, route);
    }
}
