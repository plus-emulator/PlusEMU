using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // (1,2) is void inside the map; (6,3) is floor that only void surrounds.
    private const string UnreachableGoalRoom = "00000000\r00000000\r0x000xxx\r00000x0x\r00000xxx";

    [Theory]
    [InlineData(3, 3)]
    [InlineData(1, 2)]
    [InlineData(6, 3)]
    [InlineData(20, 20)]
    public async Task AClickWithNoRouteKeepsTheCurrentWalk(int x, int y)
    {
        var (actor, move) = UnreachableGoalWalk();
        await move.Parse(_client, ClientPacket(x, y));
        ExecutorTick();
        Assert.True(actor.HasStatus("mv"));
        FallbackRun(actor, 10);
        FallbackAssertStopped(actor, 7, 1);
    }

    [Fact]
    public async Task AClickOnAReachableTileBehindStillTurnsTheWalk()
    {
        var (actor, move) = UnreachableGoalWalk();
        await move.Parse(_client, ClientPacket(0, 3));
        FallbackRun(actor, 10);
        FallbackAssertStopped(actor, 0, 3);
    }

    [Fact]
    public async Task AStandingAvatarStaysOnAClickWithNoRoute()
    {
        FallbackModel(UnreachableGoalRoom);
        Add(30, 3, 3, height: 1, stackable: false);
        var actor = FallbackActor(0, 1);
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(3, 3));
        FallbackRun(actor, 3);
        FallbackAssertStopped(actor, 0, 1);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 3)]
    public async Task AStandingAvatarGetsNoStatusOnAClickWithNoRoute(int x, int y)
    {
        FallbackModel(UnreachableGoalRoom);
        Add(30, 3, 3, height: 1, stackable: false);
        Add(31, 1, 1, height: 1, stackable: false);
        var actor = FallbackActor(0, 1);
        FallbackRun(actor, 3);
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(x, y));
        var updates = new List<bool>();

        for (var tick = 0; tick < 4; tick++) {
            ExecutorTick();
            updates.Add(ExecutorHasUpdate(actor));
        }

        Assert.Equal([false, false, false, false], updates);
        FallbackAssertStopped(actor, 0, 1);
    }

    private (RoomUser Actor, MoveAvatarEvent Move) UnreachableGoalWalk()
    {
        FallbackModel(UnreachableGoalRoom);
        Add(30, 3, 3, height: 1, stackable: false);
        var actor = FallbackActor(0, 1);
        var move = new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!));
        move.Parse(_client, ClientPacket(7, 1)).GetAwaiter().GetResult();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("mv"));

        return (actor, move);
    }
}
