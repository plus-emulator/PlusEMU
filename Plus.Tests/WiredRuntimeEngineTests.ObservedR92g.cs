using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredRuntimeEngineTests
{
    // The called stack's message comes in the caller's tick, and its action flashes while its own trigger does not.
    [Fact]
    public void CalledStackFlashesItsActionsButNotItsTrigger()
    {
        var f = new Fixture();
        var calledTrigger = f.Trigger(WiredEventKind.Signal, x: 5);
        var calledAction = f.Action(x: 5);
        var callerTrigger = f.Trigger();
        var caller = f.Action(ctx => ctx.Operations.CallStacks(ctx, [calledTrigger.Item]));

        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

        Assert.Equal(1, calledAction.Calls);
        Assert.Contains(calledAction.Item, f.Flashed);
        Assert.Contains(caller.Item, f.Flashed);
        Assert.Contains(callerTrigger.Item, f.Flashed);
        Assert.DoesNotContain(calledTrigger.Item, f.Flashed);
    }

    // The order of two stacks on one say is not fixed by id.
    [Fact]
    public void StacksOfOneEventRunInAVaryingOrder()
    {
        var firsts = new HashSet<int>();

        for (var seed = 0; seed < 16; seed++) {
            var f = new Fixture(effectOrderRandom: new Random(seed));
            var order = new List<int>();
            f.Trigger(x: 0);
            f.Action(_ => { order.Add(0); return true; }, x: 0);
            f.Trigger(x: 1);
            f.Action(_ => { order.Add(1); return true; }, x: 1);

            Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));
            Assert.Equal(2, order.Count);
            firsts.Add(order[0]);
        }

        Assert.Equal([0, 1], firsts.Order());
    }

    // Delay 0 at the firing, precise -50 about 50 ms later, one pulse 500 ms after the firing.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-50, 50)]
    [InlineData(1, 500)]
    public void ActionDelaysCountFromTheFiring(int delay, long milliseconds)
    {
        Assert.Equal(milliseconds, Plus.HabboHotel.Items.Wired.Configuration.WiredConfigurationLimits.DelayMilliseconds(delay));
        var f = new Fixture();
        f.Trigger();
        var action = f.Action(delay: delay);
        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

        if (milliseconds > 0) {
            f.Advance((int)milliseconds - 1);
            Assert.Equal(0, action.Calls);
            f.Advance(1);
        }

        Assert.Equal(1, action.Calls);
    }
}
