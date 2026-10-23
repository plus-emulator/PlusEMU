using System.Reflection;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests
{
    private static WiredModernCondition ValidMovesBox(Room room)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_valid_moves", out var descriptor));
        var item = Floor(60, 5, 5);
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, room);
        var box = new WiredModernCondition(room, item, descriptor, TestGroupManager.Empty, _ => null, () => DateTimeOffset.UnixEpoch);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);

        return box;
    }

    // rel_mov dy -1 of a marker on the wall row: valid_moves fails and the stack does not run. The same with a second, free marker still fails. A legal stack move passes.
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void ValidMovesChecksTheStacksOwnMoves(bool blocked, bool secondFree, bool passes)
    {
        var (room, map, items) = World();
        var mover = Floor(1, 2, blocked ? 0 : 2);
        var free = Floor(2, 4, 3);
        Place(map, items, mover);
        Place(map, items, free);
        var action = Box(room, "wf_act_rel_mov", [1, 0, 0, 1, 100], secondFree ? [mover.Id, free.Id] : [mover.Id]);
        var condition = ValidMovesBox(room);
        var context = Context(room, [mover, free], []);
        context.Capture([condition, action]);

        Assert.Equal(passes, condition.Execute(context));
    }

    // A column of three markers moving dy -1 into the north wall, picked m3, m2, m1: m1 stays, m2 stacks on m1 (z 1.0), m3 lands on m2 before it leaves and keeps z 1.0 on (8,2). The next fire puts m3 on top (z 2.0).
    [Fact]
    public void RelativeMoveColumnIntoAWallStacksInPickOrder()
    {
        var (room, map, items) = World();
        var m1 = Floor(1, 2, 0);
        var m2 = Floor(2, 2, 1);
        var m3 = Floor(3, 2, 2);

        foreach (var marker in new[] { m1, m2, m3 }) {
            marker.Definition.Stackable = true;
            marker.Definition.Height = 1;
            Place(map, items, marker);
        }

        var action = Box(room, "wf_act_rel_mov", [1, 0, 0, 1, 100], [m3.Id, m2.Id, m1.Id]);

        Assert.True(action.Execute(Context(room, [m3, m2, m1], [])));
        Assert.Equal((2, 0, 0.0), (m1.GetX, m1.GetY, m1.GetZ));
        Assert.Equal((2, 0, 1.0), (m2.GetX, m2.GetY, m2.GetZ));
        Assert.Equal((2, 1, 1.0), (m3.GetX, m3.GetY, m3.GetZ));

        Assert.True(action.Execute(Context(room, [m3, m2, m1], [])));
        Assert.Equal((2, 0, 2.0), (m3.GetX, m3.GetY, m3.GetZ));
    }

    // A marker at z 1.0 moved onto a tile_stackmagic drops to the magic height (0.0) without physics and keeps z 1.0 with keepAltitude. Moved off with keepAltitude it keeps z 1.0 over the bare floor.
    [Theory]
    [InlineData(false, 0.0, 0.0)]
    [InlineData(true, 1.0, 1.0)]
    public void RaisedMarkerOntoAndOffAStackMagicTile(bool keepAltitude, double onMagic, double offMagic)
    {
        var (room, map, items) = World();
        var mover = Floor(1, 1, 2);
        mover.SetState(1, 2, 1.0);
        var magic = Floor(2, 2, 2);
        magic.Definition.InteractionType = InteractionType.Stacktool;
        magic.Definition.Stackable = true;
        Place(map, items, mover);
        Place(map, items, magic);
        var action = Box(room, "wf_act_rel_mov", [1, 1, 1, 0, 100], [mover.Id]);
        Plus.HabboHotel.Items.Wired.Runtime.WiredRuntimeContext Run()
        {
            var context = Context(room, [mover, magic], []);

            if (keepAltitude) {
                context.Policy.Addons.Physics = new(true, new HashSet<uint>(), new HashSet<int>(), new HashSet<uint>());
            }

            return context;
        }

        Assert.True(action.Execute(Run()));
        Assert.Equal((2, 2, onMagic), (mover.GetX, mover.GetY, mover.GetZ));
        Assert.True(action.Execute(Run()));
        Assert.Equal((3, 2, offMagic), (mover.GetX, mover.GetY, mover.GetZ));
    }
}
