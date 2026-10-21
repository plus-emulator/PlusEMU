using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;
using static Plus.Tests.WiredR103EditorSave;

namespace Plus.Tests;

// Condition editor saves as Habbo stores them: out-of-range values are clamped, not refused. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR103ConditionEditorTests
{
    [Fact]
    public void ActorDirectionMaskAboveTheLastBitIsStoredAs255AndNegativeAsZero()
    {
        foreach (var mask in new[] { 0, 1, 2, 4, 8, 16, 32, 64, 128, 255 }) {
            Assert.Equal([mask], Saved("wf_cnd_actor_dir", [mask]).Ints);
        }

        foreach (var mask in new[] { 256, 257, 511, 512, 65535, int.MaxValue }) {
            Assert.Equal([255], Saved("wf_cnd_actor_dir", [mask]).Ints);
        }

        foreach (var mask in new[] { -1, -2, int.MinValue }) {
            Assert.Equal([0], Saved("wf_cnd_actor_dir", [mask]).Ints);
        }

        Assert.Equal([0], Saved("wf_cnd_actor_dir", []).Ints);
        Assert.Equal([2], Saved("wf_cnd_actor_dir", [2, 3]).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_actor_in_team")]
    [InlineData("wf_cnd_not_in_team")]
    public void TeamNumbersAboveFourAreStoredAsFourAndNegativeAsZero(string name)
    {
        foreach (var team in new[] { 0, 1, 2, 3, 4 }) {
            Assert.Equal([team], Saved(name, [team]).Ints);
        }

        foreach (var team in new[] { 5, 6, 100, int.MaxValue }) {
            Assert.Equal([4], Saved(name, [team]).Ints);
        }

        Assert.Equal([0], Saved(name, [-1]).Ints);
        Assert.Equal([0], Saved(name, []).Ints);
        Assert.Equal([1], Saved(name, [1, 2]).Ints);
    }

    [Fact]
    public void CounterTimeClampsEachFieldAndKeepsComparisonThreeUnclamped()
    {
        const string name = "wf_cnd_counter_time_matches";

        foreach (var seconds in new[] { 60, int.MaxValue }) {
            Assert.Equal([59, 0, 0, 0], Saved(name, [seconds, 0, 0, 0]).Ints);
        }

        foreach (var minutes in new[] { 100, int.MaxValue }) {
            Assert.Equal([0, 99, 0, 0], Saved(name, [0, minutes, 0, 0]).Ints);
        }

        Assert.Equal([0, 0, 0, 0], Saved(name, [-1, -1, -1, -1]).Ints);
        Assert.Equal([0, 0, 1, 0], Saved(name, [0, 0, 2, 0]).Ints);

        foreach (var comparison in new[] { 0, 1, 2, 3 }) {
            Assert.Equal([1, 0, 0, comparison], Saved(name, [1, 0, 0, comparison]).Ints);
        }

        Assert.Equal([5, 0, 0, 0], Saved(name, [5]).Ints);
        Assert.Equal([0, 0, 0, 0], Saved(name, []).Ints);
        Assert.Equal([1, 2, 0, 0], Saved(name, [1, 2, 0, 0, 7]).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_furnis_hv_avtrs", 1)]
    [InlineData("wf_cnd_not_furni_on", 0)]
    [InlineData("wf_cnd_not_hv_avtrs", 0)]
    public void AnyNonZeroOptionIsStoredAsOne(string name, int emptyDefault)
    {
        Assert.Equal([0], Saved(name, [0]).Ints);
        Assert.Equal([1], Saved(name, [1]).Ints);

        foreach (var option in new[] { 2, -1, 100, int.MinValue }) {
            Assert.Equal([1], Saved(name, [option]).Ints);
        }

        Assert.Equal([emptyDefault], Saved(name, []).Ints);
        Assert.Equal([1], Saved(name, [1, 0]).Ints);
    }

    [Fact]
    public void AltitudeClampsToZeroTo8000AndKeepsComparisonThree()
    {
        const string name = "wf_cnd_has_altitude";

        foreach (var altitude in new[] { 8001, 100000, int.MaxValue }) {
            Assert.Equal([8000, 1], Saved(name, [altitude, 1]).Ints);
        }

        foreach (var altitude in new[] { -1, -100 }) {
            Assert.Equal([0, 1], Saved(name, [altitude, 1]).Ints);
        }

        Assert.Equal([100, 0], Saved(name, [100, -1]).Ints);

        foreach (var comparison in new[] { 0, 1, 2, 3 }) {
            Assert.Equal([100, comparison], Saved(name, [100, comparison]).Ints);
        }

        Assert.Equal([50, 0], Saved(name, [50]).Ints);
        Assert.Equal([0, 0], Saved(name, []).Ints);
        Assert.Equal([1, 1], Saved(name, [1, 1, 1]).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_match_snapshot")]
    [InlineData("wf_cnd_not_match_snap")]
    public void OnlyTheExactValueOneSwitchesASnapshotFlagOn(string name)
    {
        Assert.Equal([1, 1, 1, 1], Saved(name, [1, 1, 1, 1]).Ints);
        Assert.Equal([0, 0, 0, 0], Saved(name, [2, 2, 2, 2]).Ints);
        Assert.Equal([0, 0, 0, 0], Saved(name, [-1, -1, -1, -1]).Ints);
        Assert.Equal([1, 0, 0, 0], Saved(name, [1]).Ints);
        Assert.Equal([0, 0, 0, 0], Saved(name, []).Ints);
        Assert.Equal([1, 1, 1, 1], Saved(name, [1, 1, 1, 1, 1]).Ints);
    }

    [Fact]
    public void UserCountClampsEachEndAndRaisesTheMaximumToTheMinimum()
    {
        const string name = "wf_cnd_not_user_count";

        foreach (var value in new[] { 126, 1000, int.MaxValue }) {
            Assert.Equal([125, 125], Saved(name, [value, value]).Ints);
            Assert.Equal([1, 125], Saved(name, [1, value]).Ints);
        }

        Assert.Equal([0, 0], Saved(name, [-1, -1]).Ints);
        Assert.Equal([10, 10], Saved(name, [10, 5]).Ints);
        Assert.Equal([125, 125], Saved(name, [126, 5]).Ints);
        Assert.Equal([5, 5], Saved(name, [5, -1]).Ints);
        Assert.Equal([5, 50], Saved(name, [5]).Ints);
        Assert.Equal([1, 50], Saved(name, []).Ints);
        Assert.Equal([1, 2], Saved(name, [1, 2, 3]).Ints);
    }
}
