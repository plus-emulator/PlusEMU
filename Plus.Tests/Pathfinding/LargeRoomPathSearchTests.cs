using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests.Pathfinding;

public class LargeRoomPathSearchTests
{
    [Theory]
    [InlineData(1026, 1)]
    [InlineData(1, 1026)]
    public void LongRoomsKeepEveryStepInTheShortestRoute(int width, int height)
    {
        var (grid, _, _) = NavTest.Create(width, height);
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);
        var route = new Route();
        var result = new PathSearch(grid, new()).Find(
            new(new(), new(0, 0, 0), width - 1, height - 1), lease.Workspace, route);

        Assert.Equal(PathOutcome.Found, result);
        Assert.Equal(1025, route.Count);
        Assert.Equal(Enumerable.Range(1, 1025), route.Steps.ToArray().Select(step => step.Tile));
    }

    [Fact]
    public void PathPriorityKeepsCostAheadOfLargeDistanceAndInsertionOrder()
    {
        Assert.True(PathTieBreak.Key(1, 2000, 0, 0, 0, 0, 0).CompareTo(
            PathTieBreak.Key(2, 0, 0, 0, 0, 0, 0)) < 0);
        Assert.True(PathTieBreak.Key(1, 0, 0, 0, 0, 0, 1 << 22).CompareTo(
            PathTieBreak.Key(1, 0, 0, 0, 0, 1, 0)) < 0);
    }
}
