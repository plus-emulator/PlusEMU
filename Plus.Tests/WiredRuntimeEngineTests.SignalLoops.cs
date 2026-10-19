using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: a receiving stack runs at most 100 times in one 500 ms wired window, all in the tick
// of the first signal. Signals past that are dropped, but the sending antenna still flips. A straight chain runs every stack once.
public partial class WiredRuntimeEngineTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ASignalLoopRunsEachReceivingStackAHundredTimesInOneWindow(int stacks)
    {
        var f = new Fixture();
        var (say, receivers, antennas) = SignalStacks(f, stacks, loop: true);
        Say(f, say);

        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));

        for (var i = 0; i < 60; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));
        Assert.Equal(101, f.Flashed.Count(item => ReferenceEquals(item, antennas[0])));
        Assert.All(antennas.Skip(1).Take(stacks - 1), antenna => Assert.Equal(100, f.Flashed.Count(item => ReferenceEquals(item, antenna))));
        Assert.False(f.Engine.NeedsFastCycle);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ASecondSignalLoopInTheNextWindowRunsAnotherHundredTimes()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 2, loop: true);
        Say(f, say);
        f.Advance(550);
        Say(f, say);

        Assert.Equal(750, f.Now);
        Assert.All(receivers, receiver => Assert.Equal(200, receiver.Calls));

        for (var i = 0; i < 60; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(200, receiver.Calls));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ASecondSignalLoopInTheSameWindowIsDropped()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 2, loop: true);
        Say(f, say);
        f.Advance(150);
        Say(f, say);

        Assert.Equal(350, f.Now);
        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));

        for (var i = 0; i < 60; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));
        Assert.False(f.Engine.NeedsFastCycle);
        Assert.Empty(f.Errors);
    }

    // A loop too large to finish in one pass still stops: the 100 runs count for the whole signal chain, not only the window.
    [Fact]
    public void ALargeSignalLoopStopsAfterAHundredRunsPerStack()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 50, loop: true);
        Say(f, say);

        for (var i = 0; i < 400; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));
        Assert.False(f.Engine.NeedsFastCycle);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ASignalLoopThatCrossesAWindowStillRunsAHundredTimesPerStack()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 20, loop: true);

        while (f.Now % 500 != 400) {
            f.Advance(50);
        }

        Say(f, say);

        for (var i = 0; i < 200; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(100, receiver.Calls));
        Assert.False(f.Engine.NeedsFastCycle);
        Assert.Empty(f.Errors);
    }

    // Observed on AIR WIN63-202610151745-318700621: a receiving stack that signals its own antenna after a 0.5 s delay kept running past
    // 100 runs (88, 107, 126, 144 at 9 s steps), about two runs a second.
    [Fact]
    public void ASignalLoopWithADelayKeepsRunningPastAHundredRuns()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 1, loop: true, delay: 1);
        Say(f, say);

        for (var i = 0; i < 1400; i++) {
            f.Advance(50);
        }

        Assert.InRange(receivers[0].Calls, 120, 160);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void AStraightSignalChainLongerThanTheDepthLimitRunsEveryStackOnce()
    {
        var f = new Fixture();
        var (say, receivers, _) = SignalStacks(f, 25, loop: false);
        Say(f, say);

        Assert.All(receivers, receiver => Assert.Equal(1, receiver.Calls));

        for (var i = 0; i < 60; i++) {
            f.Advance(50);
        }

        Assert.All(receivers, receiver => Assert.Equal(1, receiver.Calls));
        Assert.Empty(f.Errors);
    }

    // Raises the Enter event and steps the room until the say stack has run. Receivers that ran in its tick are counted.
    private static void Say(Fixture f, Box say)
    {
        var calls = say.Calls;
        Assert.True(f.Engine.Enqueue(new(WiredEventKind.Enter)));

        for (var i = 0; i < 10 && say.Calls == calls; i++) {
            f.Advance(50);
        }

        Assert.Equal(calls + 1, say.Calls);
    }

    // An Enter stack signals A1. Receiving stack Ri picks Ai and signals A(i+1); the last one signals A1 in a loop, or else an antenna
    // that no stack picks.
    private static (Box Say, Box[] Receivers, Item[] Antennas) SignalStacks(Fixture f, int count, bool loop, int delay = 0)
    {
        var antennas = Enumerable.Range(0, count + 1).Select(i => f.Furni("antenna", x: 40 + i)).ToArray();
        var receivers = new Box[count];

        for (var i = 0; i < count; i++) {
            var target = loop && i == count - 1 ? antennas[0] : antennas[i + 1];
            ModernTrigger(f, "wf_trg_recv_signal", new() { IntParams = [0, 100], SelectedItems = [antennas[i].Id] }, x: 1 + i);
            receivers[i] = f.Action(ctx => f.Engine.SendSignal(ctx, [target], new()), delay: delay, x: 1 + i);
        }

        f.Trigger();
        var say = f.Action(ctx => f.Engine.SendSignal(ctx, [antennas[0]], new()));

        return (say, receivers, antennas);
    }
}
