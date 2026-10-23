using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // With n the number of whole 0.5 second pulses
    // since the reset took effect, Less Than T passes while n is at most T and More Than T passes while n is at least T.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheTimeConditionsBothPassDuringThePulseOfTheirThreshold(int threshold)
    {
        var timer = new RoomTimerWorld();
        var resetAt = RoomLoadedAt.AddMinutes(11);
        timer.Reset(resetAt, monotonicMilliseconds: 10_150);

        for (var pulse = 0; pulse <= 5; pulse++) {
            timer.Now = resetAt.AddMilliseconds(350 + pulse * 500 + 250);

            Assert.True(pulse <= threshold == timer.Check("wf_cnd_time_less_than", threshold), $"less than {threshold} in pulse {pulse}");
            Assert.True(pulse >= threshold == timer.Check("wf_cnd_time_more_than", threshold), $"more than {threshold} in pulse {pulse}");
        }
    }

    // A negative threshold is stored; Less Than never passes and More Than always passes.
    [Fact]
    public void ANegativeTimeThresholdNeverPassesLessThanAndAlwaysPassesMoreThan()
    {
        var timer = new RoomTimerWorld();
        var resetAt = RoomLoadedAt.AddMinutes(11);
        timer.Reset(resetAt, monotonicMilliseconds: 10_150);

        foreach (var offset in new[] { 600, 1100, 2600 }) {
            timer.Now = resetAt.AddMilliseconds(offset);

            Assert.False(timer.Check("wf_cnd_time_less_than", -1));
            Assert.True(timer.Check("wf_cnd_time_more_than", -1));
        }
    }
}
