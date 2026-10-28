using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Furni a user moves onto a roller waits until one room tick has passed before the roller takes it.
    [Theory]
    [InlineData(0, 0, 2)]
    [InlineData(3, 3, 5)]
    [InlineData(3, 2, 2)]
    public async Task FurniMovedOntoARollerRollsOnlyAfterAFullRoomTick(int speed, int cycle, int slideTick)
    {
        FallbackModel(EdgeRoom);
        ExecutorRoller(10, 2, 1);
        var cargo = ExecutorFloor(20, 0, 1, height: 0.2);
        FallbackActor(0, 6);
        var handler = _room.GetRoomItemHandler();
        handler.SetSpeed(speed);
        handler.GotRollers = true;
        typeof(RoomItemHandling).GetField("_mRollerCycle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(handler, cycle);
        await MoveObject().Parse(_room, _client, ClientPacket(20, 2, 1, 0));
        Assert.Equal((2, 1), (cargo.GetX, cargo.GetY));
        var slid = -1;

        for (var tick = 1; tick <= 8 && slid < 0; tick++) {
            ExecutorTick();

            if (_client.Packets.Any(packet => packet.Header == ServerPacketHeader.SlideObjectBundleComposer)) {
                slid = tick;
            }
        }

        Assert.Equal(slideTick, slid);
        Assert.Equal((3, 1), (cargo.GetX, cargo.GetY));
    }
}
