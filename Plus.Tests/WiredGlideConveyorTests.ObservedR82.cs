using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Give Score and Give Score To Team in a room without a running game.
public sealed partial class WiredGlideConveyorTests
{
    private static string ObservedNativeDefault(string name, int[] owned)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return "native:" + JsonSerializer.Serialize(WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. owned] });
    }

    private static (Fixture Fixture, RoomUser Speaker, Plus.HabboHotel.Items.Item Marker) ObservedScoreRoom(string giveName, int[] giveInts, int[] triggerInts)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, giveName, ObservedNativeDefault(giveName, giveInts)),
            (43, 6, 12, 0.0, 0, "wf_trg_score_achieved", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [.. triggerInts] })),
            (44, 6, 12, 0.65, 0, "wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [0, 1], [ObservedSubjectId], 100))
        };
        var f = new Fixture(layout, live: true);

        return (f, f.Walker(1, 4, 10, 4, 11), f.Items[ObservedSubjectId]);
    }

    // With no game running, the say gave 1 point (team 0, own user) and 1 point to team 1, both boxes flashed, and neither Score
    // Achieved stack (threshold 1) moved the marker. Once a team game runs the same stacks score and fire.
    [Theory]
    [InlineData("wf_act_give_score", new[] { 1, 0 }, new[] { 1, 0 })]
    [InlineData("wf_act_give_score_tm", new[] { 1, 0, 1 }, new[] { 1, 1 })]
    public void GiveScoreIsANoOpUntilATeamGameRuns(string giveName, int[] giveInts, int[] triggerInts)
    {
        var (f, speaker, marker) = ObservedScoreRoom(giveName, giveInts, triggerInts);
        speaker.Team = Plus.HabboHotel.Rooms.Games.Teams.Team.Red;
        var start = (marker.GetX, marker.GetY);

        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);

        Assert.Equal(start, (marker.GetX, marker.GetY));
        Assert.All(f.Room.GetGameManager().Points, points => Assert.Equal(0, points));
        Assert.Equal("1", f.Items[ObservedActionId].LegacyDataString);

        f.Room.GetSoccer().StartGame();
        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);

        Assert.Equal((start.Item1, start.Item2 + 1), (marker.GetX, marker.GetY));
    }
}
