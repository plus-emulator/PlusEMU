using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredRuntimeEngineTests
{
    // The sender's action after send_signal always runs before both receiver stacks, and the receivers run in the same tick.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void SignalReceiversRunAfterTheSendersRemainingActionsInTheSameTick(int seed)
    {
        var f = new Fixture(effectOrderRandom: new Random(seed));
        var antenna = f.Furni("antenna", x: 2);
        var order = new List<string>();
        f.Trigger();
        f.Action(ctx => { order.Add("send"); return ctx.Operations.SendSignal(ctx, [antenna], new()); });
        f.Action(_ => { order.Add("after"); return true; });
        f.Trigger(WiredEventKind.Signal, x: 2);
        f.Action(_ => { order.Add("hop one"); return true; }, x: 2);
        f.Trigger(WiredEventKind.Signal, x: 3);
        f.Action(_ => { order.Add("hop two"); return true; }, x: 3);

        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

        Assert.Equal(4, order.Count);
        Assert.True(order.IndexOf("after") < order.IndexOf("hop one") && order.IndexOf("after") < order.IndexOf("hop two"), string.Join(",", order));
    }

    // A delayed action flashes with its stack when the stack fires, not when it runs.
    [Fact]
    public void DelayedActionFlashesWhenItsStackFires()
    {
        var f = new Fixture();
        f.Trigger();
        var delayed = f.Action(delay: 2);

        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

        Assert.Equal(0, delayed.Calls);
        Assert.Contains(delayed.Item, f.Flashed);
        f.Advance(1000);
        Assert.Equal(1, delayed.Calls);
        Assert.Single(f.Flashed, item => item == delayed.Item);
    }
}
