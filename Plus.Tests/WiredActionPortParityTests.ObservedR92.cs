using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    // From (11,1) with the owner on (4,4) (Manhattan 10) the marker still moves +x.
    [Fact]
    public void FleeHasNoRangeAndStepsEastFromAFarOwner()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 4, 1);
        Place(map, items, mover);
        var owner = Avatar(room, 1, 0, 4);
        var action = Box(room, "wf_act_flee", [100], [mover.Id]);

        Assert.True(action.Execute(Context(room, [mover], [owner])));
        Assert.Equal((5, 1), (mover.GetX, mover.GetY));
    }

    // Flee moves from (4,2) to (4,1). Against the north wall at (4,1) with the owner straight south, the marker goes to (5,1).
    [Theory]
    [InlineData(2, 1, 2, 0)]
    [InlineData(2, 0, 3, 0)]
    public void FleeStepsAwayOrSidestepsToTheRightAlongAWall(int fromX, int fromY, int expectedX, int expectedY)
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, fromX, fromY);
        Place(map, items, mover);
        var owner = Avatar(room, 1, 2, 3);
        var action = Box(room, "wf_act_flee", [100], [mover.Id]);

        Assert.True(action.Execute(Context(room, [mover], [owner])));
        Assert.Equal((expectedX, expectedY), (mover.GetX, mover.GetY));
    }

    // Manhattan 10 still takes one random cardinal step. An owner two tiles along the row is followed every time.
    [Fact]
    public void ChaseStepsTowardsANearOwnerAndWandersOneTileWhenNoneIsInRange()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var near = Floor(1, 3, 1);
        Place(map, items, near);
        var owner = Avatar(room, 1, 1, 1);
        Assert.True(Box(room, "wf_act_chase", [100], [near.Id]).Execute(Context(room, [near], [owner])));
        Assert.Equal((2, 1), (near.GetX, near.GetY));

        var (farRoom, farMap, farItems) = World();
        var far = Floor(2, 2, 2);
        Place(farMap, farItems, far);
        var distant = Avatar(farRoom, 1, 5, 5);
        Assert.True(Box(farRoom, "wf_act_chase", [100], [far.Id]).Execute(Context(farRoom, [far], [distant])));
        Assert.Equal(1, Math.Abs(far.GetX - 2) + Math.Abs(far.GetY - 2));
    }

    // With nobody to follow, the chase's random step never goes straight back.
    [Fact]
    public void ChaseRandomStepNeverReversesTheLastStep()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 4, 4);
        Place(map, items, mover);
        var action = Box(room, "wf_act_chase", [100], [mover.Id]);
        (int X, int Y)? last = null;

        for (var fire = 0; fire < 40; fire++) {
            var (x, y) = (mover.GetX, mover.GetY);
            action.Execute(Context(room, [mover], []));
            var step = (mover.GetX - x, mover.GetY - y);

            if (step == (0, 0)) {
                continue;
            }

            Assert.NotEqual((-last?.X, -last?.Y), (step.Item1, step.Item2));
            last = step;
        }

        Assert.NotNull(last);
    }

    // rel_mov +1x onto an own stackable marker lands on top of it (z 2.0 on a marker at 1.0 with height 1.0), with or without a physics add-on.
    [Fact]
    public void RelativeMoveStacksOntoStackableFurnitureInTheWay()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 1, 1);
        var block = Floor(2, 2, 1);
        block.Definition.Stackable = true;
        block.Definition.Height = 1;
        Place(map, items, mover);
        Place(map, items, block);
        var action = Box(room, "wf_act_rel_mov", [1, 1, 1, 0, 100], [mover.Id]);

        Assert.True(action.Execute(Context(room, [mover], [])));
        Assert.Equal((2, 1, 1.0), (mover.GetX, mover.GetY, mover.GetZ));
    }

    // user_to_furni onto a solid (non-walkable) marker puts the owner on top of it (z 1.0).
    [Fact]
    public void UserToFurniStandsTheUserOnTopOfASolidTarget()
    {
        var (room, map, items) = World();
        using var roomOwnerScope = RoomOwnerScope.Enter(room);
        var block = Floor(1, 3, 3);
        block.Definition.Stackable = true;
        block.Definition.Walkable = false;
        block.Definition.Height = 1;
        Place(map, items, block);
        var owner = Avatar(room, 1, 0, 0);
        Register(room, map, owner);
        var action = Box(room, "wf_act_user_to_furni", [100, 0, 1], [block.Id]);

        Assert.True(action.Execute(Context(room, [block], [owner], owner)));
        Assert.Equal((3, 3, 1.0), (owner.X, owner.Y, owner.Z));
    }

    [Theory]
    [InlineData("wf_act_chase", "0,100,200,201", "", "100", "", "")]
    [InlineData("wf_act_flee", "0,100,200,201", "", "100", "", "")]
    [InlineData("wf_act_move_to_dir", "0,100,200,201", "", "100", "", "0,0,1")]
    [InlineData("wf_act_rel_mov", "0,100,200,201", "", "100", "", "0,0")]
    [InlineData("wf_act_furni_to_furni", "0,100,101,200,201;0,100,101,200,201", "", "0,100", "", "")]
    [InlineData("wf_act_send_signal", "0,100,101,200,201;0,100,101,200,201", "0,200,201", "100,200", "200", "0,0")]
    [InlineData("wf_act_user_to_furni", "0,100,200,201", "0,200,201", "100", "0", "0")]
    [InlineData("wf_xtra_mov_physics", "0,100,101,200,201,900;0,100,101,200,201,900", "0,200,201,900", "900,900", "900", "1,0,0,0")]
    [InlineData("wf_xtra_mov_carry_users", "", "0,200,201,900", "", "900", "0")]
    [InlineData("wf_xtra_anim_time", "", "", "", "", "500")]
    [InlineData("wf_xtra_mov_curve", "0,100,200,201", "0,200,201", "100", "0", "0,160,-20")]
    [InlineData("wf_trg_recv_signal", "100,200,201", "", "100", "", "")]
    [InlineData("wf_trg_period_short", "", "", "", "", "4")]
    [InlineData("wf_trg_walks_on_furni", "100,200", "", "100", "", "")]
    public void MovementEditorFramesMatchTheObservedInitialFrames(string name, string furniAllowed, string usersAllowed,
        string furniDefaults, string userDefaults, string owned)
    {
        var metadata = WiredNativeEditorProjection.Metadata(name);
        string Groups(System.Collections.Immutable.ImmutableArray<System.Collections.Immutable.ImmutableArray<int>> groups) =>
            string.Join(';', groups.Select(group => string.Join(',', group)));

        Assert.Equal(furniAllowed, Groups(metadata.FurniAllowed));
        Assert.Equal(usersAllowed, Groups(metadata.UsersAllowed));
        Assert.Equal(furniDefaults, string.Join(',', metadata.FurniDefaults));
        Assert.Equal(userDefaults, string.Join(',', metadata.UserDefaults));
        Assert.Equal(owned, string.Join(',', metadata.OwnedDefaults));
    }
}
