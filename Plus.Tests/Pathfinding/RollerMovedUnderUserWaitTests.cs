using System.Reflection;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A roller a user moves under a standing user waits one room tick before it carries that user, like furni a user moves onto a roller.
    [Fact]
    public async Task ARollerMovedUnderAStandingUserCarriesItOnlyAfterAFullRoomTick()
    {
        FallbackModel(EdgeRoom);
        var roller = ExecutorRoller(10, 6, 5);
        var actor = FallbackActor(2, 1);
        var handler = _room.GetRoomItemHandler();
        handler.SetSpeed(0);
        handler.GotRollers = true;
        typeof(RoomItemHandling).GetField("_mRollerCycle", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(handler, 0);

        await MoveObject().Parse(_room, _client, ClientPacket(10, 2, 1, 2));
        Assert.Equal((2, 1), (roller.GetX, roller.GetY));
        var slid = -1;

        for (var tick = 1; tick <= 8 && slid < 0; tick++) {
            ExecutorTick();

            if (LandingAvatarSlides(actor).Count > 0) {
                slid = tick;
            }
        }

        Assert.Equal(2, slid);
    }
}
