using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Furni a user places from the inventory onto a roller waits one room tick, like furni a user moves.
    [Fact]
    public async Task FurniPlacedOntoARollerRollsOnlyAfterAFullRoomTick()
    {
        FallbackModel(EdgeRoom);
        ExecutorRoller(10, 2, 1);
        var definition = Furni(20, InteractionType.None, WiredBoxType.None).Definition;
        definition.Walkable = true;
        definition.Height = 0.2;
        Inventory(new InventoryItem { Id = 20, Definition = definition });
        FallbackActor(0, 6);
        var handler = _room.GetRoomItemHandler();
        handler.SetSpeed(0);
        handler.GotRollers = true;
        typeof(RoomItemHandling).GetField("_mRollerCycle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(handler, 0);
        await PlaceObject().Parse(_room, _client, ClientPacket("20 2 1 0"));
        var cargo = handler.GetItem(20);
        Assert.NotNull(cargo);
        Assert.Equal((2, 1), (cargo.GetX, cargo.GetY));
        var slid = -1;

        for (var tick = 1; tick <= 8 && slid < 0; tick++) {
            ExecutorTick();

            if (_client.Packets.Any(packet => packet.Header == ServerPacketHeader.SlideObjectBundleComposer)) {
                slid = tick;
            }
        }

        Assert.Equal(2, slid);
        Assert.Equal((3, 1), (cargo.GetX, cargo.GetY));
    }

    // Furni that Wired moves onto a roller waits one room tick as well.
    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    public void FurniMovedOntoARollerByWiredRollsOnlyAfterAFullRoomTick(int? rotation)
    {
        var cargo = WiredRollerCargo(1, 1);
        Assert.True(WiredRoomOperations.MoveItem(_room, cargo, 2, 1, rotation));
        Assert.Equal((2, 1), (cargo.GetX, cargo.GetY));

        Assert.Equal(2, TicksUntilItMoves(cargo));
        Assert.Equal((3, 1), (cargo.GetX, cargo.GetY));
    }

    // Wired that only turns cargo on a roller does not move it to another tile, so the roller does not wait.
    [Fact]
    public void FurniRotatedInPlaceOnARollerByWiredStillRollsOnTheFirstRoomTick()
    {
        var cargo = WiredRollerCargo(2, 1);
        Assert.True(WiredRoomOperations.MoveItem(_room, cargo, 2, 1, 2));
        Assert.Equal(2, cargo.Rotation);

        Assert.Equal(1, TicksUntilItMoves(cargo));
        Assert.Equal((3, 1), (cargo.GetX, cargo.GetY));
    }

    // Wired that only changes the altitude of cargo on a roller does not move it to another tile, so the roller does not wait.
    [Fact]
    public void FurniLoweredInPlaceOnARollerByWiredStillRollsOnTheFirstRoomTick()
    {
        WiredRollerCargo(2, 1);
        var upper = ExecutorFloor(21, 2, 1, 0.7, 0.2);
        Assert.Equal(0.7, upper.GetZ, 3);
        Assert.True(WiredRoomOperations.MoveItem(_room, upper, 2, 1, height: 0.5));
        Assert.Equal(0.5, upper.GetZ, 3);

        Assert.Equal(1, TicksUntilItMoves(upper));
        Assert.Equal((3, 1), (upper.GetX, upper.GetY));
    }

    private Item WiredRollerCargo(int x, int y)
    {
        FallbackModel(EdgeRoom);
        ExecutorRoller(10, 2, 1);
        var cargo = ExecutorFloor(20, x, y, height: 0.2);
        FallbackActor(0, 6);
        var handler = _room.GetRoomItemHandler();
        handler.SetSpeed(0);
        handler.GotRollers = true;
        typeof(RoomItemHandling).GetField("_mRollerCycle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(handler, 0);

        return cargo;
    }

    private int TicksUntilItMoves(Item cargo)
    {
        var start = (cargo.GetX, cargo.GetY);

        for (var tick = 1; tick <= 8; tick++) {
            ExecutorTick();

            if ((cargo.GetX, cargo.GetY) != start) {
                return tick;
            }
        }

        return -1;
    }
}
