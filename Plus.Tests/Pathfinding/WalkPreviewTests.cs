using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void WalkPreviewShowsTheDetourWithoutMovingTheAvatar()
    {
        FallbackModel(FallbackAlternative);
        Add(21, 3, 1, height: 1, stackable: false);
        var actor = FallbackActor(0, 1);
        var route = _room.GetGameMap().Navigation!.PreviewWalk(actor, 6, 1);

        Assert.NotNull(route);
        Assert.Contains(route, p => p.Y == 3);
        Assert.DoesNotContain((3, 1), route);
        Assert.Equal((6, 1), route[^1]);
        Assert.False(actor.Movement.HasIntent);
        Assert.False(actor.SetStep);
        actor.MoveTo(6, 1);
        var walked = FallbackRun(actor, 40).Select(p => (p.X, p.Y)).Distinct().Where(p => p != (0, 1));
        Assert.Equal(route, walked);
    }
    [Fact]
    public void WalkPreviewDoesNotReturnAPartialRouteToAnUnreachableTile()
    {
        FallbackModel(FallbackDeadEnd);
        var actor = FallbackActor(0, 1);

        var route = _room.GetGameMap().Navigation!.PreviewWalk(actor, 3, 3);

        Assert.NotNull(route);
        Assert.Empty(route);
        Assert.False(actor.Movement.HasIntent);
    }
    [Fact]
    public async Task WalkPreviewCoalescesGoalsAndIgnoresRequestsForAnotherRoom()
    {
        FallbackModel(FallbackAlternative);
        var actor = FallbackActor(0, 1);
        var handler = new WalkPreviewEvent();
        await handler.Parse(_client, ClientPacket(1, (int)RoomId, 1, 1));
        await handler.Parse(_client, ClientPacket(2, (int)RoomId, 2, 1));
        await handler.Parse(_client, ClientPacket(3, (int)RoomId + 1, 6, 1));
        ExecutorTick();

        var packet = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, p => p.Header == ServerPacketHeader.WalkPreviewComposer).Body };
        Assert.Equal(new[] { 2, (int)RoomId, 2, 1, 0, 1, 2, 1, 1, 2, 1 },
            Enumerable.Range(0, 11).Select(_ => packet.ReadInt()));
        Assert.False(actor.Movement.HasIntent);
    }
    [Fact]
    public void WalkPreviewSignalsWhenTheSearchBudgetRunsOut()
    {
        FallbackModel(FallbackAlternative);
        var actor = FallbackActor(0, 1, new() { MaxExpansionsPerSearch = 1 });

        Assert.Null(_room.GetGameMap().Navigation!.PreviewWalk(actor, 6, 1));
    }
}
