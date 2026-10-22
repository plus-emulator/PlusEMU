using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const string EdgeRoom = "0000000000\r0000000000\r0000000000\r0000000000\r0000000000\r0000000000\r0000000000\r0000000000";

    [Fact]
    public async Task AClickOnATowerTooHighToClimbKeepsTheWalk()
    {
        var (actor, move) = EdgeWalk(() => ExecutorFloor(20, 5, 4, height: 2.4));
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
    }

    [Theory]
    [InlineData(5, 4)]
    [InlineData(6, 4)]
    public async Task AClickOnARaisedAreaWithoutStairsKeepsTheWalk(int x, int y)
    {
        var (actor, move) = EdgeWalk(() =>
        {
            ExecutorFloor(20, 5, 4, height: 1.6);
            ExecutorFloor(21, 6, 4, height: 1.6);
        });
        await move.Parse(_client, ClientPacket(x, y));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
    }

    [Fact]
    public async Task AClickOnARaisedTileWithAStairTurnsTheWalk()
    {
        var (actor, move) = EdgeWalk(() =>
        {
            ExecutorFloor(20, 5, 4, height: 1.6);
            ExecutorFloor(21, 4, 4, height: 0.8);
        });
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 5, 4);
        Assert.Equal(1.6, actor.Z, 3);
    }

    [Theory]
    [InlineData(9, 7)]
    [InlineData(8, 7)]
    public async Task AClickBehindOrOnAClosedGateKeepsTheWalk(int x, int y)
    {
        var (actor, move) = EdgeWalk(() =>
        {
            FallbackSetGate(FallbackGate(20, 8, 7), open: false);
            Add(21, 8, 6, height: 1, stackable: false);
            Add(22, 9, 6, height: 1, stackable: false);
        });
        await move.Parse(_client, ClientPacket(x, y));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
    }

    [Fact]
    public async Task AClickBehindAnOpenGateTurnsTheWalk()
    {
        var (actor, move) = EdgeWalk(() =>
        {
            FallbackGate(20, 8, 7);
            Add(21, 8, 6, height: 1, stackable: false);
            Add(22, 9, 6, height: 1, stackable: false);
        });
        await move.Parse(_client, ClientPacket(9, 7));
        FallbackRun(actor, 14);
        FallbackAssertStopped(actor, 9, 7);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(1, 7)]
    public async Task AClickBehindOrOnAOneWayGateKeepsTheWalk(int x, int y)
    {
        var (actor, move) = EdgeWalk(() =>
        {
            Add(20, 1, 7, height: 1, stackable: false, type: InteractionType.OneWayGate);
            Add(21, 0, 6, height: 1, stackable: false);
            Add(22, 1, 6, height: 1, stackable: false);
        });
        await move.Parse(_client, ClientPacket(x, y));
        FallbackRun(actor, 12);
        FallbackAssertStopped(actor, 9, 1);
    }

    [Fact]
    public async Task AClickOnAFreeChairTurnsTheWalkAndSits()
    {
        var (actor, move) = EdgeWalk(() => Add(20, 5, 4, height: 1, stackable: false, seat: true));
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 12);
        Assert.Equal((5, 4), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("sit"));
    }

    [Fact]
    public async Task AClickWithNoRouteOnTheLastStepLetsTheWalkArrive()
    {
        FallbackModel(EdgeRoom);
        ExecutorFloor(20, 5, 4, height: 2.4);
        var actor = FallbackActor(0, 1);
        var move = EdgeMove();
        await move.Parse(_client, ClientPacket(2, 1));
        ExecutorTick();
        ExecutorTick();
        Assert.Equal("2,1,0.0", actor.Statusses.GetValueOrDefault("mv"));
        await move.Parse(_client, ClientPacket(5, 4));
        FallbackRun(actor, 6);
        FallbackAssertStopped(actor, 2, 1);
    }

    private static MoveAvatarEvent EdgeMove() => new(new RoomAvatarActionService(TimeProvider.System, null!, null!));

    private (RoomUser Actor, MoveAvatarEvent Move) EdgeWalk(Action furnish)
    {
        FallbackModel(EdgeRoom);
        furnish();
        var actor = FallbackActor(0, 1);
        var move = EdgeMove();
        move.Parse(_client, ClientPacket(9, 1)).GetAwaiter().GetResult();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("mv"));

        return (actor, move);
    }
}
