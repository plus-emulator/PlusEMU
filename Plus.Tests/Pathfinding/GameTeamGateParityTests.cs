using System.Reflection;
using System.Text;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users.Effects;
using Xunit;

namespace Plus.Tests;

// Banzai and Freeze gates and score counters of one colour belong to one team.
public partial class PlacedFurniRoomTests
{
    private RoomUser JoinableActor(int x = 0)
    {
        var actor = ExecutorActor(x, 1);
        Assert.IsType<EffectsComponent>(_client.GetHabbo().Effects).Init(_client.GetHabbo());

        return actor;
    }

    private void StepTo(RoomUser actor, int x)
    {
        actor.MoveTo(x, 1);

        for (var tick = 0; tick < 6 && actor.X != x; tick++) {
            ExecutorTick();
        }

        Assert.Equal(x, actor.X);
    }

    private static List<(int Id, string Data)> DataItems(byte[] body)
    {
        var at = 0;

        int Int()
        {
            var value = (body[at] << 24) | (body[at + 1] << 16) | (body[at + 2] << 8) | body[at + 3];
            at += 4;

            return value;
        }

        var result = new List<(int, string)>();

        for (var count = Int(); count > 0; count--) {
            var id = Int();
            Int();
            var length = (body[at] << 8) | body[at + 1];
            at += 2;
            result.Add((id, Encoding.UTF8.GetString(body, at, length)));
            at += length;
        }

        return result;
    }

    private IEnumerable<uint> TeamHeaders() => _client.Packets.Select(packet => packet.Header).Where(header =>
        header is ServerPacketHeader.YouArePlayingGameComposer or ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.AvatarEffectComposer);

    private byte[] PacketBody(uint header) => Assert.Single(_client.Packets, packet => packet.Header == header).Body;

    private bool Locked(int x, int y)
    {
        ExecutorTick();
        var navigation = _room.GetGameMap().Navigation!;

        return (navigation.Grid.Flags[navigation.Grid.Tile(x, y)] & NavFlags.FloorLocked) != 0;
    }

    private Item GameTimer(uint id, InteractionType kind, string name)
    {
        var timer = InteractionItem(id, 3, 3, kind);
        timer.Definition.ItemName = name;
        timer.Definition.InteractionName = "game_timer";
        timer.LegacyDataString = "30";
        _room.GetWired().AttachRoomItem(timer);

        return timer;
    }

    [Theory]
    [InlineData(InteractionType.Banzaigatered, 33)]
    [InlineData(InteractionType.Banzaigategreen, 34)]
    [InlineData(InteractionType.Banzaigateblue, 35)]
    [InlineData(InteractionType.Banzaigateyellow, 36)]
    [InlineData(InteractionType.FreezeRedGate, 40)]
    [InlineData(InteractionType.FreezeGreenGate, 41)]
    [InlineData(InteractionType.FreezeBlueGate, 42)]
    [InlineData(InteractionType.FreezeYellowGate, 43)]
    public void JoiningAGateSendsPlayingGateCountAndEffectInThatOrder(InteractionType kind, int effect)
    {
        var gate = InteractionItem(10, 1, 1, kind);
        gate.Definition.Walkable = true;
        var actor = JoinableActor();
        _client.Packets.Clear();

        StepTo(actor, 1);

        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer, ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.AvatarEffectComposer],
            TeamHeaders());
        Assert.Equal(1, PacketBody(ServerPacketHeader.YouArePlayingGameComposer).Single());
        Assert.Equal([(10, "1")], DataItems(PacketBody(ServerPacketHeader.ObjectsDataUpdateComposer)));
        Assert.Equal(effect, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Equal("1", gate.LegacyDataString);
    }

    [Fact]
    public void SteppingOnAnyGateInATeamLeavesAndNeverSwitchesColour()
    {
        var blue = InteractionItem(10, 1, 1, InteractionType.Banzaigateblue);
        blue.Definition.Walkable = true;
        var red = InteractionItem(11, 2, 1, InteractionType.FreezeRedGate);
        red.Definition.Walkable = true;
        var actor = JoinableActor();
        StepTo(actor, 1);
        _client.Packets.Clear();

        StepTo(actor, 2);

        Assert.Equal(Team.None, actor.Team);
        Assert.Equal(0, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Equal("0", blue.LegacyDataString);
        Assert.Equal("0", red.LegacyDataString);
        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer, ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.AvatarEffectComposer],
            TeamHeaders());
        Assert.Equal(0, PacketBody(ServerPacketHeader.YouArePlayingGameComposer).Single());
    }

    [Fact]
    public void LeavingByTheSameColourOtherGameGateClearsTheEffect()
    {
        var banzai = InteractionItem(10, 1, 1, InteractionType.Banzaigateblue);
        banzai.Definition.Walkable = true;
        var freeze = InteractionItem(11, 2, 1, InteractionType.FreezeBlueGate);
        freeze.Definition.Walkable = true;
        var actor = JoinableActor();
        StepTo(actor, 1);

        StepTo(actor, 2);

        Assert.Equal(Team.None, actor.Team);
        Assert.Equal(0, _client.GetHabbo().Effects.CurrentEffect);
    }

    [Fact]
    public void BanzaiAndFreezeGatesOfOneColourShowTheSameCountInOnePacket()
    {
        var banzai = InteractionItem(10, 1, 1, InteractionType.Banzaigateblue);
        banzai.Definition.Walkable = true;
        var freeze = InteractionItem(11, 3, 1, InteractionType.FreezeBlueGate);
        freeze.Definition.Walkable = true;
        var other = InteractionItem(12, 0, 3, InteractionType.FreezeRedGate);
        var actor = JoinableActor();
        _client.Packets.Clear();

        StepTo(actor, 1);

        Assert.Equal("1", banzai.LegacyDataString);
        Assert.Equal("1", freeze.LegacyDataString);
        Assert.Equal("0", other.LegacyDataString);
        Assert.Equal([(10, "1"), (11, "1")], DataItems(PacketBody(ServerPacketHeader.ObjectsDataUpdateComposer)).OrderBy(item => item.Id));
    }

    [Fact]
    public void BanzaiAndFreezeScoreCountersOfOneColourShowTheTeamScoreAndFootballCountersStay()
    {
        var banzai = InteractionItem(10, 0, 3, InteractionType.Banzaiscoreblue);
        var freeze = InteractionItem(11, 1, 3, InteractionType.Freezebluecounter);
        var football = InteractionItem(12, 2, 3, InteractionType.Footballcounterblue);
        football.LegacyDataString = "4";
        _ = JoinableActor();
        _client.Packets.Clear();

        _room.GetGameManager().AddPointToTeam(Team.Blue, 7);

        Assert.Equal("7", banzai.LegacyDataString);
        Assert.Equal("7", freeze.LegacyDataString);
        Assert.Equal("4", football.LegacyDataString);
        Assert.Equal([(10, "7"), (11, "7")], DataItems(PacketBody(ServerPacketHeader.ObjectsDataUpdateComposer)).OrderBy(item => item.Id));
    }

    [Fact]
    public void ABanzaiGameStartResetsBothCounterKindsAndTheEndKeepsTheScore()
    {
        var banzai = InteractionItem(10, 0, 3, InteractionType.Banzaiscorered);
        var freeze = InteractionItem(11, 1, 3, InteractionType.Freezeredcounter);
        _room.GetGameManager().AddPointToTeam(Team.Red, 9);
        Assert.Equal("9", freeze.LegacyDataString);

        _room.GetBanzai().BanzaiStart();

        Assert.Equal("0", banzai.LegacyDataString);
        Assert.Equal("0", freeze.LegacyDataString);
        _room.GetGameManager().AddPointToTeam(Team.Red, 3);
        _room.GetBanzai().BanzaiEnd(true);
        Assert.Equal("3", banzai.LegacyDataString);
        Assert.Equal("3", freeze.LegacyDataString);
    }

    [Theory]
    [InlineData(InteractionType.Banzaicounter, "bb_counter")]
    [InlineData(InteractionType.Freezetimer, "es_counter")]
    public void AnyGameTimerStartLocksBothGameGatesPausingKeepsThemLockedAndResetUnlocksThem(InteractionType kind, string name)
    {
        InteractionItem(10, 1, 1, InteractionType.Banzaigateblue).Definition.Walkable = true;
        InteractionItem(11, 2, 1, InteractionType.FreezeRedGate).Definition.Walkable = true;
        using var owner = RoomOwnerScope.Enter(_room);
        var timer = GameTimer(20, kind, name);

        Assert.True(_room.GetWired().TryUseCounter(timer, 1));
        Assert.True(Locked(1, 1));
        Assert.True(Locked(2, 1));

        Assert.True(_room.GetWired().TryUseCounter(timer, 1));
        Assert.True(Locked(1, 1));
        Assert.True(Locked(2, 1));

        Assert.True(_room.GetWired().TryUseCounter(timer, 2));
        Assert.False(Locked(1, 1));
        Assert.False(Locked(2, 1));
    }

    [Fact]
    public void TheEndOfTheCountdownUnlocksTheGates()
    {
        InteractionItem(10, 1, 1, InteractionType.Banzaigateblue).Definition.Walkable = true;
        using var owner = RoomOwnerScope.Enter(_room);
        var timer = GameTimer(20, InteractionType.Banzaicounter, "bb_counter");
        var clocks = (WiredCounterController)typeof(WiredComponent).GetField("_counters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(_room.GetWired())!;
        Assert.True(_room.GetWired().TryUseCounter(timer, 1));
        Assert.True(_room.GetBanzai().IsBanzaiActive);
        Assert.True(Locked(1, 1));

        for (var now = 1L << 40; now < (1L << 40) + 40_000; now += 1000) {
            clocks.Poll(now);
        }

        Assert.False(clocks.IsRunning(timer));
        Assert.False(Locked(1, 1));
    }

    [Fact]
    public void ASoloFreezeStartRemovesThePlayerFromTheTeam()
    {
        var banzaiGate = InteractionItem(10, 3, 1, InteractionType.Banzaigateblue);
        banzaiGate.Definition.Walkable = true;
        var freezeGate = InteractionItem(11, 2, 1, InteractionType.FreezeBlueGate);
        freezeGate.Definition.Walkable = true;
        var actor = JoinableActor();
        StepTo(actor, 2);
        Assert.Equal(Team.Blue, actor.Team);
        Assert.Equal(42, _client.GetHabbo().Effects.CurrentEffect);
        _client.Packets.Clear();

        _room.GetFreeze().StartGame();

        Assert.Equal(Team.None, actor.Team);
        Assert.Equal(0, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Equal("0", freezeGate.LegacyDataString);
        Assert.Equal("0", banzaiGate.LegacyDataString);
        Assert.Equal(0, PacketBody(ServerPacketHeader.YouArePlayingGameComposer).Single());
    }

    [Fact]
    public void ASoloBanzaiStartKeepsThePlayerInTheTeam()
    {
        var gate = InteractionItem(10, 1, 1, InteractionType.Banzaigateblue);
        gate.Definition.Walkable = true;
        var actor = JoinableActor();
        StepTo(actor, 1);

        _room.GetBanzai().BanzaiStart();

        Assert.Equal(Team.Blue, actor.Team);
        Assert.Equal(35, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Equal("1", gate.LegacyDataString);
    }
}
