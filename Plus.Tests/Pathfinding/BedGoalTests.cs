using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests.Pathfinding;

public class BedGoalTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void EveryBedTileSelectsItsOwnLaneEvenFromTheOtherPillow(int rotation, bool layered)
    {
        var (grid, inputs, compiler) = NavTest.Create(5, 5, new() { LayeringEnabled = layered });
        var across = rotation is 2 or 6;
        int[] footprint = across ? [6, 7, 8, 11, 12, 13] : [6, 7, 11, 12, 16, 17];
        inputs.Publish(NavTest.Record(1, 1, footprint, h: 1.8, walkable: false, interaction: InteractionType.Bed) with { Rotation = rotation });
        compiler.ApplyNow();
        var occupancy = new PlanningOccupancy(grid.SlotCapacity);

        foreach (var tile in footprint) {
            var firstLane = across ? tile / 5 == 1 : tile % 5 == 1;
            var pillow = firstLane ? 6 : across ? 11 : 7;
            var other = firstLane ? across ? 11 : 7 : 6;
            var goal = GoalResolver.ResolveClick(grid, new(), grid.Position(other), tile % 5, tile / 5, occupancy);
            Assert.Equal((pillow % 5, pillow / 5), (goal.X, goal.Y));
            Assert.Equal(new SurfaceRef(pillow, 1, SurfaceKind.BedBase), grid.Reference(goal.Slot));

            occupancy.Targets[goal.Slot] = TargetOccupancy.Stationary;
            var fallback = GoalResolver.ResolveClick(grid, new(), grid.Position(other), tile % 5, tile / 5, occupancy);
            Assert.Equal((other % 5, other / 5), (fallback.X, fallback.Y));
            occupancy.Targets[goal.Slot] = TargetOccupancy.None;
        }
    }
}
