using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScoreAchievedDoesNotFireAgainInTheSameRound(bool saveIdenticalTrigger)
    {
        // Dropping below the threshold and saving the same settings do not rearm it.
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
        GiveRoundScore(f, speaker, -3);
        Assert.Equal(4, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);

        if (saveIdenticalTrigger) {
            SaveRoundBox(f, 43, [6, 1]);
        }

        GiveRoundScore(f, speaker, 2);

        Assert.Equal(6, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedFiresAgainAfterTheGameCounterRestarts()
    {
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);

        f.Owned(() => Assert.True(Counters(f).Control(f.Items[CounterId], 0, 1550, WiredClockOrigin.ModernWired)));
        f.Advance(50);
        Assert.Equal(0, Points(f, Team.Red));
        GiveRoundScore(f, speaker, 7);

        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 2, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedFiresAtTheExactThresholdButNotAboveOrOnDecreases()
    {
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 5);
        Assert.Equal(5, Points(f, Team.Red));
        Assert.Equal(start, marker.GetY);
        GiveRoundScore(f, speaker, 1);
        Assert.Equal(6, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
        GiveRoundScore(f, speaker, 1);
        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
        GiveRoundScore(f, speaker, -3);
        Assert.Equal(4, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedForAnyTeamFiresOnceForTheWholeBox()
    {
        // With team Any, Red 0->7 fired and Green 0->7 right after did not.
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        SaveRoundBox(f, 43, [6, 0]);
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(start + 1, marker.GetY);

        GiveRoundScore(f, speaker, 7, Team.Green);

        Assert.Equal(7, Points(f, Team.Green));
        Assert.Equal(start + 1, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedFiresAgainAfterItsSettingsChange()
    {
        // After Red fired, saving team Any let a Green crossing fire in the same round.
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(start + 1, marker.GetY);

        SaveRoundBox(f, 43, [6, 0]);
        GiveRoundScore(f, speaker, 7, Team.Green);

        Assert.Equal(start + 2, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedFiresAgainAfterItsSettingsChangeAndChangeBack()
    {
        // After Red fired, saving Any and then Red again let a Red recross fire.
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(start + 1, marker.GetY);

        SaveRoundBox(f, 43, [6, 0]);
        SaveRoundBox(f, 43, [6, 1]);
        GiveRoundScore(f, speaker, -3);
        GiveRoundScore(f, speaker, 2);

        Assert.Equal(6, Points(f, Team.Red));
        Assert.Equal(start + 2, marker.GetY);
    }

    [Fact]
    public void ScoreAchievedBlockedByAConditionStillUsesItsOneFiring()
    {
        // A crossing stopped by a false condition was not retried by a later recross.
        var (f, speaker, marker) = ScoreRoundRoom(withUserCountCondition: true);
        var start = marker.GetY;
        GiveRoundScore(f, speaker, 7);
        Assert.Equal(start, marker.GetY);

        f.Walker(2, 1, 1, 1, 2);
        GiveRoundScore(f, speaker, -3);
        GiveRoundScore(f, speaker, 2);
        Assert.Equal(6, Points(f, Team.Red));
        Assert.Equal(start, marker.GetY);

        f.Owned(() => Assert.True(Counters(f).Control(f.Items[CounterId], 0, 1550, WiredClockOrigin.ModernWired)));
        f.Advance(50);
        GiveRoundScore(f, speaker, 7);

        Assert.Equal(start + 1, marker.GetY);
    }

    private static (Fixture Fixture, RoomUser Speaker, Item Marker) ScoreRoundRoom(bool withUserCountCondition = false)
    {
        (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] layout =
        [
            (CounterId, 8, 12, 0, 0, "wf_game_upcounter1", null),
            (ObservedSubjectId, 9, 9, 0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_act_give_score_tm", ObservedNativeDefault("wf_act_give_score_tm", [7, 0, 1])),
            (43, 6, 12, 0, 0, "wf_trg_score_achieved", ObservedNativeDefault("wf_trg_score_achieved", [6, 1])),
            (44, 6, 12, 0.65, 0, "wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [0, 1], [ObservedSubjectId], 100))
        ];

        if (withUserCountCondition) {
            layout = [.. layout, (45, 6, 12, 1.3, 0, "wf_cnd_user_count_in", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [2, 50, 0] }))];
        }

        var f = new Fixture(layout, live: true);
        var speaker = f.Walker(1, 4, 10, 4, 11);
        speaker.Team = Team.Red;
        f.Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        f.Owned(() => Assert.True(Counters(f).Control(f.Items[CounterId], 0, 0, WiredClockOrigin.ModernWired)));
        f.Advance(50);
        Assert.True(WiredGameState.GameRunning(f.Room));
        Assert.Equal(0, Points(f, Team.Red));

        return (f, speaker, f.Items[ObservedSubjectId]);
    }

    private static void GiveRoundScore(Fixture f, RoomUser speaker, int amount, Team team = Team.Red)
    {
        SaveRoundBox(f, ObservedActionId, [amount, 0, (int)team]);
        SayAlpha(f, speaker);
    }

    private static void SaveRoundBox(Fixture f, uint itemId, int[] owned)
    {
        var box = Assert.IsAssignableFrom<IWiredConfiguredItem>(f.Box(itemId));
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { OwnedIntParams = [.. owned] };
        Assert.True(WiredNativeEditorProjection.TryCompile(itemId, box.Descriptor, native, out var configuration));
        f.Owned(() => Assert.True(WiredConfigurationSave.TrySave(box, configuration, new SavedConfigurations([]), out var error,
            publish: f.Room.GetWired().PublishConfigured), error));
        Assert.Equal(owned, box.Configuration.Origin!.Native.OwnedIntParams.ToArray());
    }
}
