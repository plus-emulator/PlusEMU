using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// At Given Time and Periodically Long, read back the way Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR102TimerTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 1)]
    [InlineData(600, 600)]
    [InlineData(1200, 1200)]
    [InlineData(1201, 1201)]
    [InlineData(100000, 100000)]
    public void AtGivenTimeStoresZeroAndHasNoUpperLimit(int sent, int stored) => Assert.Equal([stored], Saved("wf_trg_at_given_time", [sent]).Ints);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(120, 120)]
    [InlineData(121, 121)]
    [InlineData(10000, 10000)]
    public void PeriodicallyLongStoresOneAndUpAndHasNoUpperLimit(int sent, int stored) => Assert.Equal([stored], Saved("wf_trg_period_long", [sent]).Ints);

    [Theory]
    [InlineData("wf_trg_at_given_time", 1)]
    [InlineData("wf_trg_period_long", 10)]
    public void TheTimersFillMissingIntsDropExtraOnesAndIgnoreText(string name, int frameDefault)
    {
        Assert.Equal([frameDefault], Saved(name, []).Ints);
        Assert.Equal([4], Saved(name, [4, 5]).Ints);
        var withText = Saved(name, [4], text: "abc");
        Assert.Equal([4], withText.Ints);
        Assert.Equal("", withText.Text);
        Assert.Equal("wiredfurni.error.invalid_furni_source_count", Save(name, [4], furni: [100]).Key);
        Assert.Equal("wiredfurni.error.invalid_user_source_count", Save(name, [4], users: [0]).Key);
    }

    [Theory]
    [InlineData("wf_trg_at_given_time", 0, true)]
    [InlineData("wf_trg_at_given_time", -1, false)]
    [InlineData("wf_trg_at_given_time", 100000, true)]
    [InlineData("wf_trg_period_long", 0, false)]
    [InlineData("wf_trg_period_long", 121, true)]
    [InlineData("wf_trg_period_long", 10000, true)]
    public void AStoredTimerKeepsLoadingWithTheUnboundedValues(string name, int units, bool valid) =>
        Assert.Equal(valid, WiredTriggerConfiguration.TryValidate(name, new() { IntParams = [units] }, out _, out _));

    [Fact]
    public void AtGivenTimeWithTheMostPulsesFiresSixHundredAndOneSecondsAfterTheStartTick()
    {
        var timers = new WiredTimedTriggers();
        var config = new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1201] };
        Assert.False(timers.TryFire("wf_trg_at_given_time", 1, config, 600_499, 600_499, 1));
        Assert.False(timers.TryFire("wf_trg_at_given_time", 1, config, 600_999, 600_499, 1));
        Assert.True(timers.TryFire("wf_trg_at_given_time", 1, config, 601_000, 600_500, 1));
    }

    [Fact]
    public void TheLongestPeriodicallyLongFiresEverySixHundredSeconds()
    {
        var timers = new WiredTimedTriggers();
        var config = new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [120] };
        Assert.False(timers.TryFire("wf_trg_period_long", 1, config, 0, 0, 0));
        Assert.False(timers.TryFire("wf_trg_period_long", 1, config, 599_999, 0, 0));
        Assert.True(timers.TryFire("wf_trg_period_long", 1, config, 600_000, 0, 0));
        Assert.False(timers.TryFire("wf_trg_period_long", 1, config, 1_199_999, 0, 0));
        Assert.True(timers.TryFire("wf_trg_period_long", 1, config, 1_200_000, 0, 0));
    }

    private static Result Saved(string name, int[] ints, string? text = null)
    {
        var result = Save(name, ints, text: text);
        Assert.True(result.Accepted, Show(result));

        return result;
    }
}
