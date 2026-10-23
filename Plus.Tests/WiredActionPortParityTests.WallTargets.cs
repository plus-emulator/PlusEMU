using System.Collections.Concurrent;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// furni_to_furni saves with a wall torch as its only target, and firing it moves nothing and sends no WiredMovements.
/// </summary>
public sealed partial class WiredActionPortParityTests
{
    private static Item WallTarget(Room room, uint id)
    {
        var item = Floor(id, 0, 0);
        item.Definition.Type = ItemType.Wall;
        item.WallCoordinates = ":w=5,0 l=8,40 r";
        item.RoomId = room.Id;
        var walls = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_wallItems", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(room.GetRoomItemHandler())!;
        walls[id] = item;

        return item;
    }

    [Fact]
    public void FurniToFurniAcceptsAWallTargetButNeverMovesToIt()
    {
        var (room, map, items) = World();
        var mover = StackableMarker(1, 2, 3);
        Place(map, items, mover);
        var wall = WallTarget(room, 2);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [mover.Id], secondary: [wall.Id]);

        Assert.False(action.Execute(Context(room, [mover, wall], [])));
        Assert.Equal((2, 3, 0.0), (mover.GetX, mover.GetY, mover.GetZ));
    }

    [Fact]
    public void FurniToFurniSkipsWallTargetsBesideFloorTargets()
    {
        for (var attempt = 0; attempt < 20; attempt++) {
            var (room, map, items) = World();
            var mover = StackableMarker(1, 2, 3);
            var target = StackableMarker(3, 4, 2);
            Place(map, items, mover);
            Place(map, items, target);
            var wall = WallTarget(room, 2);
            var action = Box(room, "wf_act_furni_to_furni", [100, 101], [mover.Id], secondary: [wall.Id, target.Id]);

            Assert.True(action.Execute(Context(room, [mover, wall, target], [])));
            Assert.Equal((4, 2), (mover.GetX, mover.GetY));
        }
    }

    [Fact]
    public void FurniToFurniSkipsWallMovers()
    {
        var (room, map, items) = World();
        var target = StackableMarker(3, 4, 2);
        Place(map, items, target);
        var wall = WallTarget(room, 2);
        var action = Box(room, "wf_act_furni_to_furni", [100, 101], [wall.Id], secondary: [target.Id]);

        Assert.False(action.Execute(Context(room, [wall, target], [])));
        Assert.Equal(":w=5,0 l=8,40 r", wall.WallCoordinates);
    }
}
