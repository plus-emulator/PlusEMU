using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: Move/Rotate with no movement and no rotation places each picked furni not already on top
// again on its own tile, on top of the rest of its stack (bottom first); the furni above it stay where they are.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(new[] { 0 }, new[] { 3.0, 1.0, 2.0 })]
    [InlineData(new[] { 2 }, new[] { 0.0, 1.0, 2.0 })]
    [InlineData(new[] { 0, 1 }, new[] { 3.0, 4.0, 2.0 })]
    public void StillMoveRotateRestacksPickedFurniOnTopOfTheirStack(int[] picked, double[] heights)
    {
        Item[] stack = [ExecutorFloor(40, 1, 1, 0, 1), ExecutorFloor(41, 1, 1, 1, 1), ExecutorFloor(42, 1, 1, 2, 1)];

        new WiredMovementActions().Execute("wf_act_move_rotate", new() { IntParams = [0, 0, 100] }, picked.Select(index => stack[index]).ToArray(), [], [],
            (item, x, y, rotation, height) => WiredRoomOperations.MoveItem(_room, item, x, y, rotation, height), (_, _, _, _, _) => false,
            (_, _) => throw new InvalidOperationException());

        Assert.Equal(heights, stack.Select(item => item.GetZ));
        Assert.All(stack, item => Assert.Equal((1, 1), (item.GetX, item.GetY)));
    }
}
