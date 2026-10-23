using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    // rel_mov onto a tile_stackmagic (height 0) under a block lands at the magic tile's height (z 0.0), with or without physics.
    [Fact]
    public void RelativeMoveOntoAStackMagicTileLandsAtItsHeight()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 1, 2);
        var magic = Floor(2, 2, 2);
        magic.Definition.InteractionType = InteractionType.Stacktool;
        magic.Definition.Stackable = true;
        var block = Floor(3, 2, 2);
        block.Definition.Stackable = true;
        block.Definition.Height = 1;
        Place(map, items, mover);
        Place(map, items, magic);
        Place(map, items, block);
        var action = Box(room, "wf_act_rel_mov", [1, 1, 1, 0, 100], [mover.Id]);

        Assert.True(action.Execute(Context(room, [mover], [])));
        Assert.Equal((2, 2, 0.0), (mover.GetX, mover.GetY, mover.GetZ));
    }

    // dy -2 into the void does not move. Onto the own marker stacks (z 1.0).
    [Fact]
    public void RelativeMoveIntoTheVoidStays()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 2, 1);
        Place(map, items, mover);
        var action = Box(room, "wf_act_rel_mov", [1, 0, 0, 2, 100], [mover.Id]);

        Assert.False(action.Execute(Context(room, [mover], [])));
        Assert.Equal((2, 1), (mover.GetX, mover.GetY));
    }

    // move_rotate_user move 2 steps the owner one tile east and leaves the rotation (-1) alone.
    [Fact]
    public void MoveRotateUserMoveTwoStepsEast()
    {
        var (room, map, _) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var owner = Avatar(room, 1, 2, 2);
        owner.RotBody = owner.RotHead = 7;
        Register(room, map, owner);
        var action = Box(room, "wf_act_move_rotate_user", [2, -1, 0], []);

        Assert.True(action.Execute(Context(room, [], [owner], owner)));
        Assert.Equal((3, 2, 7), (owner.X, owner.Y, owner.RotBody));
    }
}
