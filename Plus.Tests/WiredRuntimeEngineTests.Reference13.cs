using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class WiredRuntimeEngineTests
{
    [Fact]
    public void AtTimeLongFiresOnceAfterItsHalfSecondUnitsAndAgainAfterARoomTimerReset()
    {
        var f = new Fixture();
        var fired = new List<long>();
        var timer = RealTimer(f, "wf_trg_at_time_long", 4);
        f.Action(_ => { fired.Add(f.Now); return true; });

        // Armed at 0: the four pulses count from the room tick at 500.
        foreach (var pass in new long[] { 0, 1000, 2499 }) {
            f.Now = pass;
            f.Engine.OnFastCycle();
        }

        Assert.Empty(fired);
        f.Now = 2500;
        f.Engine.OnFastCycle();
        f.Engine.OnFastCycle();
        Assert.Equal([2500L], fired);
        f.Now = 9000;
        f.Engine.OnFastCycle();
        Assert.Equal([2500L], fired);
        f.Now = 10000;
        f.Engine.ResetTimers([timer.Item]);
        f.Now = 12499;
        f.Engine.OnFastCycle();
        Assert.Single(fired);
        f.Now = 12500;
        f.Engine.OnFastCycle();
        f.Engine.OnFastCycle();
        Assert.Equal([2500L, 12500L], fired);
        Assert.Empty(f.Errors);
    }

    // At Given Time with 4 pulses fires 2.48 s after each reset: the pulses count from the next room tick after the reset. Here
    // the room grid is at x.000 / x.500.
    [Theory]
    [InlineData(1018, 3500)]
    [InlineData(7021, 9500)]
    [InlineData(1499, 3500)]
    [InlineData(1500, 4000)]
    public void AtGivenTimeCountsItsPulsesFromTheNextRoomTickAfterAReset(long resetAt, long firesAt)
    {
        var f = new Fixture();
        var fired = new List<long>();
        var timer = RealTimer(f, "wf_trg_at_given_time", 4);
        f.Action(_ => { fired.Add(f.Now); return true; });
        f.Now = resetAt;
        f.Engine.ResetTimers([timer.Item]);

        // The trigger's chain runs on the pass after the firing; a second pass at the same instant serves it.
        for (var now = resetAt; now <= firesAt + 1000; now += 1) {
            f.Now = now;
            f.Engine.OnFastCycle();
            f.Engine.OnFastCycle();
        }

        Assert.Equal([firesAt], fired);
        Assert.Empty(f.Errors);
    }
}
