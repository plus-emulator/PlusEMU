using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: after a roller slide the standing avatar of a user gets a UserUpdate 500 ms and another
    // 1000 ms after the slide (3 of 3 slides in each of 3 runs). None comes with the slide itself, and none follows after the second.
    [Fact]
    public void ARollerSlideIsFollowedByTwoUserUpdatesOneRoomTickApart()
    {
        ExecutorRoller(10, 0, 1);
        InstallRollerChainEngine();
        var actor = RollerChainActor(1, 0, .5);
        ExecutorTick();
        EnableExecutorRollers();
        ExecutorTick();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.SlideObjectBundleComposer);
        var sent = new List<bool> { UserUpdateSent() };

        for (var tick = 0; tick < 4; tick++) {
            _room.GetRoomItemHandler().GotRollers = false;
            ExecutorTick();
            sent.Add(UserUpdateSent());
        }

        Assert.Equal([false, true, true, false, false], sent);
    }

    private bool UserUpdateSent() => _client.Packets.Any(packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
}
