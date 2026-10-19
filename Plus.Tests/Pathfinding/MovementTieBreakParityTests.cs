using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610151745-318700621: tied north/south repairs use
    // the western tile, and tied east/west repairs use the northern tile.
    [Theory]
    [InlineData(false, 7, 12, 7, 7, 7, 10, 7, 9, 6, 9)]
    [InlineData(true, 7, 12, 7, 7, 7, 10, 7, 9, 6, 9)]
    [InlineData(false, 5, 8, 8, 8, 6, 8, 7, 8, 7, 7)]
    [InlineData(true, 5, 8, 8, 8, 6, 8, 7, 8, 7, 7)]
    [InlineData(false, 10, 8, 5, 8, 9, 8, 8, 8, 8, 7)]
    [InlineData(true, 10, 8, 5, 8, 9, 8, 8, 8, 8, 7)]
    public void EqualCostRepairsPreferTheObservedCoordinateOrder(bool layered,
        int startX, int startY, int goalX, int goalY, int triggerX, int triggerY,
        int blockX, int blockY, int detourX, int detourY)
    {
        var actor = HabboParityActor(startX, startY, layered);
        var path = HabboParityWalk(actor, goalX, goalY, next =>
        {
            if (next == (triggerX, triggerY)) {
                HabboParityMove(100, blockX, blockY);
            }
        });
        var coordinates = path.Select(step => (step.X, step.Y)).ToArray();
        var trigger = Array.IndexOf(coordinates, (triggerX, triggerY));
        Assert.True(trigger >= 0);
        Assert.Equal((detourX, detourY), coordinates[trigger + 1]);
        Assert.Equal((goalX, goalY), coordinates[^1]);
        Assert.Equal(Math.Max(Math.Abs(goalX - startX), Math.Abs(goalY - startY)) + 1, path.Count);
        Assert.False(actor.IsWalking);
        Assert.False(actor.HasStatus("mv"));
    }
}
