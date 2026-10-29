using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The door tile is void in the heightmap, like the door of most room models.
    private const string DoorEastRoom = "xxxxx\r0000x\r0000x\r0000x";

    [Fact]
    public async Task ARollerSlidesTheUserOntoTheDoorTileAndTheUserStaysInTheRoom()
    {
        Set("_gamemap", new Gamemap(_room, new RoomModel("door_east", 4, 1, 0, 6, DoorEastRoom, 0, 0, false), TestLogging.Navigation,
            TestRoomSettings.Empty, new TestGroupManager(id => _groupLookup(id)), _database, TestNavigationRewards.Instance));
        _room.GetGameMap().GenerateMaps();
        ExecutorRoller(10, 3, 1);
        var actor = FallbackActor(1, 1);
        EnableExecutorRollers();
        await EdgeMove().Parse(_client, ClientPacket(3, 1));
        var slides = new List<(int ToX, string ToZ)>();

        for (var tick = 0; tick < 10 && slides.Count == 0; tick++) {
            ExecutorTick();
            slides.AddRange(LandingAvatarSlides(actor));
        }

        Assert.Equal([(4, "0.0")], slides);

        for (var tick = 0; tick < 4; tick++) {
            ExecutorTick();
        }

        Assert.Same(actor, _room.GetRoomUserManager().GetRoomUserByVirtualId(actor.VirtualId));
        Assert.Equal((4, 1), (actor.X, actor.Y));
    }
}
