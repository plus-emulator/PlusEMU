using System.Reflection;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.Tests.Performance;
using Xunit;

namespace Plus.Tests.Pathfinding;

[CollectionDefinition("Pathfinding room adapter", DisableParallelization = true)]
public class PathfindingRoomAdapterCollection;

[Collection("Pathfinding room adapter")]
public class NavigationPolicyTests
{


    [Fact]
    public void OccupiedGoalsUseActorPolicyAndDoorsRemainShared()
    {
        var (grid, _, _) = NavTest.Create(3, 1, door: 2);
        var occupancy = new PlanningOccupancy(3);
        occupancy.Targets[1] = occupancy.Targets[2] = TargetOccupancy.Stationary;
        Assert.Equal(-1, GoalResolver.Resolve(grid, new(), 1, 0, occupancy).Slot);
        Assert.Equal(1, GoalResolver.Resolve(grid, new() { IgnoreUsers = true }, 1, 0, occupancy).Slot);
        Assert.Equal(1, GoalResolver.Resolve(grid, new() { LegacyOverride = true }, 1, 0, occupancy).Slot);
        Assert.Equal(2, GoalResolver.Resolve(grid, new(), 2, 0, occupancy).Slot);
        occupancy.Targets[1] = TargetOccupancy.Walking;
        Assert.Equal(-1, GoalResolver.Resolve(grid, new(), 1, 0, occupancy).Slot);
        Assert.Equal(1, GoalResolver.Resolve(grid, new() { Walkthrough = true }, 1, 0, occupancy).Slot);
    }

    [Fact]
    public void BedClicksPreserveTheClickedLaneAndFallBackToAFreePillow()
    {
        var (grid, inputs, compiler) = NavTest.Create(4, 4);
        inputs.Publish(NavTest.Record(1, 1, [5, 6, 9, 10], h: 1, walkable: false, interaction: Plus.HabboHotel.Items.InteractionType.Bed));
        compiler.ApplyNow();
        var occupancy = new PlanningOccupancy(16);
        var goal = GoalResolver.ResolveClick(grid, new(), grid.Position(4), 2, 2, occupancy);
        Assert.Equal(6, goal.Slot);
        occupancy.Targets[6] = TargetOccupancy.Stationary;
        Assert.Equal(5, GoalResolver.ResolveClick(grid, new(), grid.Position(4), 2, 2, occupancy).Slot);
        occupancy.Targets[5] = TargetOccupancy.Stationary;
        Assert.Equal(-1, GoalResolver.ResolveClick(grid, new(), grid.Position(4), 2, 2, occupancy).Slot);
        Assert.Equal(10, GoalResolver.Resolve(grid, new() { IgnoreUsers = true }, 2, 2, occupancy).Slot);
    }

    [Fact]
    public void AlreadyThereReturnsAnEmptyRoute()
    {
        var fixture = RoomPerformanceFixture.Create(1, 0);
        var actor = fixture.Bots[0];
        actor.GoalX = actor.X;
        actor.GoalY = actor.Y;
        var navigation = NavTest.Enable(fixture.Map);
        navigation.Compiler.RebuildAll();
        var route = new Route();
        var grid = navigation.Grid;
        var outcome = new PathSearch(grid, new()).Find(new(new ActorProfile(), grid.Position(grid.Tile(actor.X, actor.Y)), actor.GoalX, actor.GoalY), new PathWorkspace(grid.SlotCapacity, grid.ActiveNodeCount), route);
        Assert.Equal(PathOutcome.AlreadyThere, outcome);
        Assert.Equal(0, route.Count);
    }

}
