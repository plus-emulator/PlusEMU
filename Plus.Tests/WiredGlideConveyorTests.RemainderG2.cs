using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

// g2: wf_act_give_score, wf_act_give_score_tm and wf_trg_score_achieved as full stacks (say trigger, give score, score trigger, marker
// move), in a room whose team game runs (official: outside a running game the score actions do nothing, see ObservedR82).
public sealed partial class WiredGlideConveyorTests
{
    private const int RedTeam = (int)Team.Red;

    private static (Fixture Fixture, RoomUser Speaker, Item Marker, int Start) RunningScoreRoom(string giveName, int[] giveInts, int[] triggerInts, Team team = Team.Red)
    {
        var (f, speaker, marker) = ObservedScoreRoom(giveName, giveInts, triggerInts);
        speaker.Team = team;
        f.Room.GetSoccer().StartGame();

        return (f, speaker, marker, marker.GetY);
    }

    private static void SayAlpha(Fixture f, RoomUser speaker)
    {
        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);
    }

    private static int Points(Fixture f, Team team) => f.Room.GetGameManager().Points[(int)team];

    [Fact]
    public void GiveScoreAddsToTheSpeakersTeamAndScoreAchievedFiresOnlyWhenTheThresholdIsCrossed()
    {
        var (f, speaker, marker, start) = RunningScoreRoom("wf_act_give_score", [3, 0], [3, 0]);
        SayAlpha(f, speaker);
        Assert.Equal(3, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY); // 0 -> 3 crosses 3.
        SayAlpha(f, speaker);
        Assert.Equal(6, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY); // 3 -> 6 does not cross it again.
        Assert.Equal(0, Points(f, Team.Blue));
    }

    [Fact]
    public void ScoreAchievedFiresWhenOnePointJumpsOverTheThreshold()
    {
        var (f, speaker, marker, start) = RunningScoreRoom("wf_act_give_score", [7, 0], [5, 0]);
        SayAlpha(f, speaker);
        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY); // Previous below the points, new score at or above them.
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, true)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    public void ScoreAchievedTeamRadioMatchesAnyTeamOrOnlyTheNamedOne(int triggerTeam, bool fires)
    {
        var (f, speaker, marker, start) = RunningScoreRoom("wf_act_give_score", [2, 0], [2, triggerTeam]);
        SayAlpha(f, speaker);
        Assert.Equal(2, Points(f, Team.Red));
        Assert.Equal(fires ? start + 1 : start, marker.GetY);
    }

    [Fact]
    public void ANegativeGiveScoreSubtractsAndNeverGoesBelowZero()
    {
        var (f, speaker, _, _) = RunningScoreRoom("wf_act_give_score", [-5, 0], [1000, 0]);
        f.Room.GetGameManager().AddPointToTeam(Team.Red, 2);
        SayAlpha(f, speaker);
        Assert.Equal(0, Points(f, Team.Red));
        SayAlpha(f, speaker);
        Assert.Equal(0, Points(f, Team.Red));
        f.Room.GetGameManager().AddPointToTeam(Team.Red, 12);
        SayAlpha(f, speaker);
        Assert.Equal(7, Points(f, Team.Red));
    }

    [Fact]
    public void GiveScoreTimesPerGameStopsAtTheLimitAndComesBackWithTheNextGame()
    {
        var (f, speaker, _, _) = RunningScoreRoom("wf_act_give_score", [4, 2], [1000, 0]);

        for (var say = 0; say < 4; say++) {
            SayAlpha(f, speaker);
        }

        Assert.Equal(8, Points(f, Team.Red)); // Two grants only.
        WiredGameState.For(f.Room).ResetQuotas();
        SayAlpha(f, speaker);
        Assert.Equal(12, Points(f, Team.Red));
    }

    [Fact]
    public void GiveScoreZeroTimesMeansUnlimited()
    {
        var (f, speaker, _, _) = RunningScoreRoom("wf_act_give_score", [1, 0], [1000, 0]);

        for (var say = 0; say < 12; say++) {
            SayAlpha(f, speaker);
        }

        Assert.Equal(12, Points(f, Team.Red));
    }

    [Fact]
    public void GiveScoreNeedsTheUserToBeOnATeam()
    {
        var (f, speaker, marker, start) = RunningScoreRoom("wf_act_give_score", [5, 0], [1, 0], Team.None);
        SayAlpha(f, speaker);
        Assert.All(f.Room.GetGameManager().Points, points => Assert.Equal(0, points));
        Assert.Equal(start, marker.GetY);
        Assert.Equal("1", f.Items[ObservedActionId].LegacyDataString); // The box still flashed: it ran, it just had nobody to score for.
    }

    [Fact]
    public void GiveScoreToTeamPaysTheNamedTeamWhateverTeamTheUserIsOn()
    {
        var (f, speaker, marker, start) = RunningScoreRoom("wf_act_give_score_tm", [6, 0, 3], [6, 3], Team.None);
        SayAlpha(f, speaker);
        Assert.Equal(6, Points(f, Team.Blue));
        Assert.Equal(0, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
    }

    [Fact]
    public void GiveScoreToTeamHonoursTheSignAndTheTimesPerGame()
    {
        var (f, speaker, _, _) = RunningScoreRoom("wf_act_give_score_tm", [-2, 3, 2], [1000, 0], Team.Red);
        f.Room.GetGameManager().AddPointToTeam(Team.Green, 20);

        for (var say = 0; say < 5; say++) {
            SayAlpha(f, speaker);
        }

        Assert.Equal(14, Points(f, Team.Green)); // 20 - 3 * 2
        Assert.Equal(0, Points(f, Team.Red));
    }

    [Theory]
    [InlineData("wf_act_give_score", new[] { 1, 0 }, true)]
    [InlineData("wf_act_give_score", new[] { -1000, 10 }, true)]
    [InlineData("wf_act_give_score", new[] { 1000, 0 }, true)]
    [InlineData("wf_act_give_score", new[] { 0, 0 }, false)]
    [InlineData("wf_act_give_score", new[] { 1001, 0 }, false)]
    [InlineData("wf_act_give_score", new[] { -1001, 0 }, false)]
    // Observed on AIR WIN63-202610151745-318700621: Habbo saves a per-game limit above 10; it is stored as 10.
    [InlineData("wf_act_give_score", new[] { 5, 11 }, true)]
    [InlineData("wf_act_give_score", new[] { 5, -1 }, false)]
    [InlineData("wf_act_give_score", new[] { 5 }, false)]
    [InlineData("wf_act_give_score_tm", new[] { 5, 0, 1 }, true)]
    [InlineData("wf_act_give_score_tm", new[] { -5, 10, 4 }, true)]
    [InlineData("wf_act_give_score_tm", new[] { 5, 0, 0 }, false)]
    [InlineData("wf_act_give_score_tm", new[] { 5, 0, 5 }, false)]
    [InlineData("wf_act_give_score_tm", new[] { 0, 0, 1 }, false)]
    [InlineData("wf_act_give_score_tm", new[] { 5, 11, 1 }, false)]
    [InlineData("wf_act_give_score_tm", new[] { 5, 0 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { 1, 0 }, true)]
    [InlineData("wf_trg_score_achieved", new[] { 1000, 4 }, true)]
    [InlineData("wf_trg_score_achieved", new[] { 100, 0 }, true)]
    [InlineData("wf_trg_score_achieved", new[] { 0, 0 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { 1001, 0 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { -3, 0 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { 5, 5 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { 5, -1 }, false)]
    [InlineData("wf_trg_score_achieved", new[] { 5 }, false)]
    public void TheScoreEditorsKeepToTheOctoberSliderRanges(string name, int[] owned, bool accepted)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. owned] };
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
    }

    [Fact]
    public void ScoreCardsOpenWithTheObservedOfficialEditorFrames()
    {
        // hhus WIN63-202610131318 r62-batch-02 / r62-batch-01: give score 5/0 with users [0,200,201] default 0, give score to team 5/0/1 with no
        // selectors, score achieved 100/0 with no selectors.
        var give = WiredNativeEditorProjection.DefaultNative(WiredBoxRegistry.All.Single(box => box.CanonicalName == "wf_act_give_score"));
        Assert.Equal([5, 0], give.OwnedIntParams.ToArray());
        Assert.Equal([0], give.UserSourceTypes.ToArray());
        Assert.Equal([0, 200, 201], Assert.Single(WiredNativeEditorProjection.Metadata("wf_act_give_score").UsersAllowed).ToArray());
        var team = WiredNativeEditorProjection.DefaultNative(WiredBoxRegistry.All.Single(box => box.CanonicalName == "wf_act_give_score_tm"));
        Assert.Equal([5, 0, 1], team.OwnedIntParams.ToArray());
        Assert.Empty(team.UserSourceTypes);
        Assert.Empty(team.FurniSourceTypes);
        var trigger = WiredNativeEditorProjection.DefaultNative(WiredBoxRegistry.All.Single(box => box.CanonicalName == "wf_trg_score_achieved"));
        Assert.Equal([100, 0], trigger.OwnedIntParams.ToArray());
        Assert.Empty(trigger.UserSourceTypes);
        Assert.Empty(trigger.FurniSourceTypes);
    }
}
