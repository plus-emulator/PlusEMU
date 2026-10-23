using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// Gate counts, gate locks and the zeroed score counters of the Banzai and Freeze games.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void ATeamMemberLeavingTheRoomLowersTheGateCount()
    {
        var gate = InteractionItem(10, 1, 1, InteractionType.Banzaigateblue);
        gate.Definition.Walkable = true;
        var actor = JoinableActor();
        StepTo(actor, 1);
        Assert.Equal("1", gate.LegacyDataString);
        _client.Packets.Clear();

        _room.GetRoomUserManager().RemoveUserFromRoom(_client, false);

        Assert.Empty(_room.GetTeamManagerForBanzai().BlueTeam);
        Assert.Equal("0", gate.LegacyDataString);
        Assert.Contains((10, "0"), DataItems(PacketBody(ServerPacketHeader.ObjectsDataUpdateComposer)));
    }

    [Fact]
    public void ResettingAnIdleTimerKeepsTheGatesLockedWhileAnotherGameTimerIsStarted()
    {
        InteractionItem(10, 1, 1, InteractionType.Banzaigateblue).Definition.Walkable = true;
        using var owner = RoomOwnerScope.Enter(_room);
        var freeze = GameTimer(20, InteractionType.Freezetimer, "es_counter");
        var banzai = GameTimer(21, InteractionType.Banzaicounter, "bb_counter");
        Assert.True(_room.GetWired().TryUseCounter(freeze, 1));
        Assert.True(Locked(1, 1));

        Assert.True(_room.GetWired().TryUseCounter(freeze, 1));
        Assert.True(_room.GetWired().TryUseCounter(banzai, 2));

        Assert.True(Locked(1, 1));
        Assert.True(_room.GetWired().TryUseCounter(freeze, 2));
        Assert.False(Locked(1, 1));
    }

    [Fact]
    public void AGameStartSendsTheZeroedScoreCountersOnce()
    {
        var counter = InteractionItem(10, 0, 3, InteractionType.Banzaiscorered);
        InteractionItem(11, 1, 3, InteractionType.Freezeredcounter);
        _ = JoinableActor();
        _room.GetGameManager().AddPointToTeam(Team.Red, 9);
        using var owner = RoomOwnerScope.Enter(_room);
        var timer = GameTimer(20, InteractionType.Banzaicounter, "bb_counter");
        _client.Packets.Clear();

        Assert.True(_room.GetWired().TryUseCounter(timer, 1));

        Assert.Equal("0", counter.LegacyDataString);
        Assert.Single(_client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer
            && DataItems(packet.Body).Any(item => item.Id == 10)));
    }

    [Fact]
    public void TeamListsCanChangeWhileGateCountsAreShown()
    {
        InteractionItem(10, 1, 1, InteractionType.Banzaigateblue).Definition.Walkable = true;
        var actor = JoinableActor();
        actor.Team = Team.Blue;
        var teams = _room.GetTeamManagerForBanzai();
        var others = Enumerable.Range(0, 3).Select(_ => new RoomUser(0, RoomId, 0, _room, null, TestChatEmotions.Unused, new TestRewardProgress())).ToArray();
        var done = false;
        var failure = (Exception?)null;
        var writer = Task.Run(() =>
        {
            while (!Volatile.Read(ref done)) {
                foreach (var other in others) {
                    lock (teams.BlueTeam) {
                        teams.BlueTeam.Add(other);
                    }
                }

                foreach (var other in others) {
                    lock (teams.BlueTeam) {
                        teams.BlueTeam.Remove(other);
                    }
                }
            }
        });

        try {
            for (var round = 0; round < 20000; round++) {
                teams.AddUser(actor);
                teams.OnUserLeave(actor);
            }
        }
        catch (Exception exception) {
            failure = exception;
        }

        Volatile.Write(ref done, true);
        writer.Wait();
        Assert.Null(failure);
    }
}
