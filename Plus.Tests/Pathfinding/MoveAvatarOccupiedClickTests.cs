using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MidWalkClickOnAStandingAvatarKeepsTheWalk(bool walkthrough)
    {
        var (actor, move) = EdgeWalk(() => { });
        var other = ExecutorAdditionalBot(5, 4, 55);
        _room.RoomBlockingEnabled = walkthrough;
        ExecutorTick();
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
        Assert.Equal((5, 4), (other.X, other.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MidWalkClickOnAnOccupiedChairKeepsTheWalk(bool walkthrough)
    {
        var (actor, move) = EdgeWalk(() => Add(20, 5, 4, height: 1, stackable: false, seat: true));
        var other = ExecutorAdditionalBot(5, 4, 55);
        _room.RoomBlockingEnabled = walkthrough;
        ExecutorTick();
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandingClickOnAStandingAvatarDoesNotMove(bool walkthrough)
    {
        FallbackModel(EdgeRoom);
        var actor = FallbackActor(0, 1);
        var other = ExecutorAdditionalBot(5, 4, 55);
        _room.RoomBlockingEnabled = walkthrough;
        ExecutorTick();
        await EdgeMove().Parse(_client, ClientPacket(5, 4));
        var trail = FallbackRun(actor, 12);
        Assert.All(trail, p => Assert.Equal((0, 1), (p.X, p.Y)));
    }
}
