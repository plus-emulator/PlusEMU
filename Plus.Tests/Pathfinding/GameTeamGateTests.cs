using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users.Effects;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(InteractionType.Banzaigateblue, Team.Blue)]
    [InlineData(InteractionType.Banzaigatered, Team.Red)]
    [InlineData(InteractionType.FreezeBlueGate, Team.Blue)]
    [InlineData(InteractionType.FreezeYellowGate, Team.Yellow)]
    public void StepOnAPlacedTeamGateJoinsItsTeam(InteractionType kind, Team team)
    {
        var gate = InteractionItem(10, 1, 1, kind);
        gate.Definition.Walkable = true;
        var actor = ExecutorActor(0, 1);
        Assert.IsType<EffectsComponent>(_client.GetHabbo().Effects).Init(_client.GetHabbo());

        actor.MoveTo(1, 1);
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Equal(team, actor.Team);
    }

    [Fact]
    public void FreezeLocksAPlacedGateAndForgetsItAfterPickup()
    {
        var gate = InteractionItem(10, 1, 1, InteractionType.FreezeBlueGate);
        gate.Definition.Walkable = true;
        var picked = InteractionItem(11, 2, 1, InteractionType.FreezeRedGate);
        picked.Definition.Walkable = true;
        _ = ExecutorActor(0, 1);
        Assert.True(_room.GetRoomItemHandler().RemoveFurniture(_client, picked));
        var navigation = _room.GetGameMap().Navigation!;

        _room.GetFreeze().StartGame();
        ExecutorTick();

        Assert.True((navigation.Grid.Flags[navigation.Grid.Tile(1, 1)] & NavFlags.FloorLocked) != 0);
        Assert.False((navigation.Grid.Flags[navigation.Grid.Tile(2, 1)] & NavFlags.FloorLocked) != 0);
    }

    [Fact]
    public void TeamPointsReachPlacedScoreCountersButNotTheGates()
    {
        var freezeCounter = InteractionItem(10, 1, 1, InteractionType.Freezebluecounter);
        var banzaiCounter = InteractionItem(11, 2, 1, InteractionType.Banzaiscoreblue);
        var gate = InteractionItem(12, 3, 1, InteractionType.Banzaigateblue);
        gate.LegacyDataString = "2";

        _room.GetGameManager().AddPointToTeam(Team.Blue, 10);

        Assert.Equal("10", freezeCounter.LegacyDataString);
        Assert.Equal("10", banzaiCounter.LegacyDataString);
        Assert.Equal("2", gate.LegacyDataString);
    }
}
