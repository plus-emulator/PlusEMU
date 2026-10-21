using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class WiredRuntimeEngineTests
{
    // Observed on AIR WIN63-202610201032-107964540: a delay that is already running is paused while the wired disabler is on. It goes on
    // with the time it had left when the disabler is off again: started at 0 s with 5 s of delay, disabled from 1.0 s to 3.5 s, the effect
    // runs at 7.5 s.
    [Fact]
    public void ARunningDelayIsPausedWhileTheWiredDisablerIsOn()
    {
        var f = new Fixture();
        var disabler = Disabler(f, on: false);
        f.Trigger();
        var action = f.Action(delay: 10);
        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));
        f.Advance(1000);

        disabler.LegacyDataString = "1";
        f.Advance(0);
        f.Advance(2500);
        Assert.Equal(0, action.Calls);

        disabler.LegacyDataString = "0";
        f.Advance(0);
        f.Advance(3500);
        Assert.Equal(0, action.Calls);

        f.Advance(500);
        Assert.Equal(1, action.Calls);
    }

    // Observed on AIR WIN63-202610201032-107964540: a delay that would have ended while the disabler was on does not run at once when it is
    // off again.
    [Fact]
    public void ADelayThatEndsWhileTheWiredDisablerIsOnWaitsForTheRestOfItsTime()
    {
        var f = new Fixture();
        var disabler = Disabler(f, on: false);
        f.Trigger();
        var action = f.Action(delay: 4);
        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));
        f.Advance(500);

        disabler.LegacyDataString = "1";
        f.Advance(0);
        f.Advance(5000);
        disabler.LegacyDataString = "0";
        f.Advance(0);
        Assert.Equal(0, action.Calls);

        f.Advance(1000);
        Assert.Equal(0, action.Calls);

        f.Advance(500);
        Assert.Equal(1, action.Calls);
    }
}
