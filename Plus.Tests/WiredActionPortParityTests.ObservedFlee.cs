using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    // The model_a floor: x 4..11 and y 1..13 are floor, everything else is void.
    private static readonly string ModelA = string.Join('\r',
        new[] { "xxxxxxxxxxxx" }.Concat(Enumerable.Repeat("xxxx00000000", 13)).Concat(["xxxxxxxxxxxx", "xxxxxxxxxxxx"]));

    // A user in the same row or column within three tiles makes the block step straight away. Otherwise, or when that tile is taken, the block steps east, even
    // towards the user. When east is taken it steps away from the user on y (south when level), then the other way on y.
    [Theory]
    [InlineData(8, 11, 5, 11, 4, 11)]
    [InlineData(8, 11, 8, 9, 8, 8)]
    [InlineData(8, 11, 4, 11, 5, 11)]
    [InlineData(8, 11, 8, 7, 9, 7)]
    [InlineData(8, 11, 7, 10, 8, 10)]
    [InlineData(8, 11, 9, 10, 10, 10)]
    [InlineData(8, 11, 7, 12, 8, 12)]
    [InlineData(8, 11, 9, 12, 10, 12)]
    [InlineData(8, 11, 6, 9, 7, 9)]
    [InlineData(8, 11, 10, 9, 11, 9)]
    [InlineData(8, 11, 6, 13, 7, 13)]
    [InlineData(8, 11, 10, 13, 11, 13)]
    [InlineData(8, 11, 6, 10, 7, 10)]
    [InlineData(8, 11, 5, 10, 6, 10)]
    [InlineData(8, 11, 8, 13, 9, 13)]
    [InlineData(8, 11, 11, 11, 11, 12)]
    [InlineData(8, 11, 11, 10, 11, 9)]
    [InlineData(8, 11, 11, 12, 11, 13)]
    [InlineData(10, 13, 7, 10, 8, 10)]
    [InlineData(10, 12, 11, 13, 11, 12)]
    [InlineData(6, 12, 9, 11, 10, 11)]
    [InlineData(6, 12, 9, 13, 10, 13)]
    [InlineData(6, 12, 8, 11, 9, 11)]
    public void FleeStepsStraightAwayFromAUserInLineAndOtherwiseEast(int ownerX, int ownerY, int fromX, int fromY, int toX, int toY)
    {
        var (room, map, items) = World(ModelA);
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, fromX, fromY);
        Place(map, items, mover);
        var owner = Avatar(room, 1, ownerX, ownerY);
        var action = Box(room, "wf_act_flee", [100], [mover.Id]);

        Assert.True(action.Execute(Context(room, [mover], [owner])));
        Assert.Equal((toX, toY), (mover.GetX, mover.GetY));
    }
}
