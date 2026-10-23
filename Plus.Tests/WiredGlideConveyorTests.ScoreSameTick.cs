using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Score Achieved stack runs in the same tick as the Give Score of the say that crossed the threshold, right after the echo
    // of the say. It does not wait for the next pass.
    [Fact]
    public void ScoreAchievedStackRunsInTheTickOfTheSayThatCrossesTheThreshold()
    {
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        SaveRoundBox(f, ObservedActionId, [7, 0, (int)Team.Red]);

        f.Owned(() => speaker.OnChat(0, "alpha", false));

        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
    }

    // The first crossing is the only one of the round, so the same-tick run still counts as the one firing.
    [Fact]
    public void ScoreAchievedStillFiresOnlyOncePerRoundWhenItRunsInTheTickOfTheScoreChange()
    {
        var (f, speaker, marker) = ScoreRoundRoom();
        var start = marker.GetY;
        SaveRoundBox(f, ObservedActionId, [7, 0, (int)Team.Red]);
        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);
        SaveRoundBox(f, ObservedActionId, [-3, 0, (int)Team.Red]);
        f.Owned(() => speaker.OnChat(0, "alpha", false));
        SaveRoundBox(f, ObservedActionId, [3, 0, (int)Team.Red]);
        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);

        Assert.Equal(7, Points(f, Team.Red));
        Assert.Equal(start + 1, marker.GetY);
    }
}
