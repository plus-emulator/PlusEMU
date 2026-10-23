using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    private static Item StackableMarker(uint id, int x, int y)
    {
        var item = Floor(id, x, y);
        item.Definition.Stackable = true;
        item.Definition.Height = 1;

        return item;
    }

    // The mover lands on top of the target (z 1.0). With two targets the pick is random.
    [Fact]
    public void FurniToFurniLandsOnTopOfARandomTarget()
    {
        var reached = new HashSet<(int, int)>();

        for (var attempt = 0; attempt < 40 && reached.Count < 2; attempt++) {
            var (room, map, items) = World();
            var mover = StackableMarker(1, 0, 0);
            var first = StackableMarker(2, 2, 2);
            var second = StackableMarker(3, 4, 2);
            Place(map, items, mover);
            Place(map, items, first);
            Place(map, items, second);
            var action = Box(room, "wf_act_furni_to_furni", [100, 101], [mover.Id], secondary: [first.Id, second.Id]);

            Assert.True(action.Execute(Context(room, [mover, first, second], [])));
            Assert.Equal(1.0, mover.GetZ);
            reached.Add((mover.GetX, mover.GetY));
        }

        Assert.Equal([(2, 2), (4, 2)], reached.Order());
    }

    // Two movers, one target: both land on it, the second on top of the first (z 1.0 and 2.0, one packet).
    [Fact]
    public void TwoMoversOntoOneTargetStack()
    {
        var (room, map, items) = World();
        var m1 = StackableMarker(1, 0, 0);
        var m2 = StackableMarker(2, 1, 0);
        var target = StackableMarker(3, 3, 3);
        Place(map, items, m1);
        Place(map, items, m2);
        Place(map, items, target);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [m1.Id, m2.Id], secondary: [target.Id]);

        Assert.True(action.Execute(Context(room, [m1, m2, target], [])));
        Assert.Equal((3, 3), (m1.GetX, m1.GetY));
        Assert.Equal((3, 3), (m2.GetX, m2.GetY));
        Assert.Equal([1.0, 2.0], new[] { m1.GetZ, m2.GetZ }.Order());
    }

    // Two movers, two targets: one each.
    [Fact]
    public void TwoMoversOntoTwoTargetsTakeOneEach()
    {
        var (room, map, items) = World();
        var m1 = StackableMarker(1, 0, 0);
        var m2 = StackableMarker(2, 1, 0);
        var t1 = StackableMarker(3, 2, 2);
        var t2 = StackableMarker(4, 4, 2);
        Place(map, items, m1);
        Place(map, items, m2);
        Place(map, items, t1);
        Place(map, items, t2);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [m1.Id, m2.Id], secondary: [t1.Id, t2.Id]);

        Assert.True(action.Execute(Context(room, [m1, m2, t1, t2], [])));
        Assert.Equal([(2, 2), (4, 2)], new[] { (m1.GetX, m1.GetY), (m2.GetX, m2.GetY) }.Order());
    }

    // A target stacked on a block (top 2.0): the mover lands at z 2.0.
    [Fact]
    public void FurniToFurniLandsOnTopOfAStackedTarget()
    {
        var (room, map, items) = World();
        var mover = StackableMarker(1, 0, 0);
        var block = StackableMarker(2, 3, 1);
        var target = StackableMarker(3, 3, 1);
        target.SetState(3, 1, 1.0);
        Place(map, items, mover);
        Place(map, items, block);
        Place(map, items, target);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [mover.Id], secondary: [target.Id]);

        Assert.True(action.Execute(Context(room, [mover, block, target], [])));
        Assert.Equal((3, 1, 2.0), (mover.GetX, mover.GetY, mover.GetZ));
    }

    // The target tile held by the owner: the mover stays.
    [Fact]
    public void FurniToFurniRefusesATargetTileHeldByAUser()
    {
        var (room, map, items) = World();
        var mover = StackableMarker(1, 0, 0);
        var target = Floor(2, 3, 1);
        target.Definition.Stackable = true;
        target.Definition.Walkable = true;
        Place(map, items, mover);
        Place(map, items, target);
        var owner = Avatar(room, 1, 3, 1);
        Register(room, map, owner);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [mover.Id], secondary: [target.Id]);

        Assert.False(action.Execute(Context(room, [mover, target], [owner])));
        Assert.Equal((0, 0), (mover.GetX, mover.GetY));
    }

    // Both sides free, owner straight south: the sidestep is east (right of the away heading) every time.
    [Fact]
    public void FleeSidestepsToTheRightWhenBothSidesAreFree()
    {
        for (var trial = 0; trial < 6; trial++) {
            var (room, map, items) = World();
            var mover = Floor(1, 2, 0);
            Place(map, items, mover);
            var owner = Avatar(room, 1, 2, 2);
            var action = Box(room, "wf_act_flee", [100], [mover.Id]);

            Assert.True(action.Execute(Context(room, [mover], [owner])));
            Assert.Equal((3, 0), (mover.GetX, mover.GetY));
        }
    }

    // Out of range in a corner (north and west blocked) the marker always moves (east or south).
    [Fact]
    public void ChaseOutOfRangeRetriesABlockedRandomStep()
    {
        for (var trial = 0; trial < 20; trial++) {
            var (room, map, items) = World();
            var mover = Floor(1, 0, 0);
            Place(map, items, mover);
            var owner = Avatar(room, 1, 5, 5);
            var action = Box(room, "wf_act_chase", [100], [mover.Id]);

            Assert.True(action.Execute(Context(room, [mover], [owner])));
            Assert.Contains((mover.GetX, mover.GetY), new[] { (1, 0), (0, 1) });
        }
    }
}
