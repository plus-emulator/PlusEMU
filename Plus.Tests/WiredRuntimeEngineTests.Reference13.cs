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

        foreach (var pass in new long[] { 0, 1000, 1999 }) {
            f.Now = pass;
            f.Engine.OnFastCycle();
        }

        Assert.Empty(fired);
        f.Now = 2000;
        f.Engine.OnFastCycle();
        f.Engine.OnFastCycle();
        Assert.Equal([2000L], fired);
        f.Now = 9000;
        f.Engine.OnFastCycle();
        Assert.Equal([2000L], fired);
        f.Now = 10000;
        f.Engine.ResetTimers([timer.Item]);
        f.Now = 11999;
        f.Engine.OnFastCycle();
        Assert.Single(fired);
        f.Now = 12000;
        f.Engine.OnFastCycle();
        f.Engine.OnFastCycle();
        Assert.Equal([2000L, 12000L], fired);
        Assert.Empty(f.Errors);
    }
}
