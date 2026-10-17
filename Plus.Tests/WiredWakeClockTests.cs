using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

[CollectionDefinition("Wired wake timing", DisableParallelization = true)]
public class WiredWakeTimingCollection;

[Collection("Wired wake timing")]
public class WiredWakeClockTests
{
    [Fact]
    public async Task DelayedWakeUpsLandOnTheirOwnDueTimeNotOnTheNextRoomStep()
    {
        using var clock = new WiredWakeClock();
        var start = WiredWakeClock.Now;
        double[] due = [1500, 500, 1000];
        var woke = new double[due.Length];
        var order = new List<int>();
        var done = new CountdownEvent(due.Length);

        for (var i = 0; i < due.Length; i++) {
            var slot = i;
            clock.At(start + due[slot], () =>
            {
                woke[slot] = WiredWakeClock.Now - start;

                lock (order) {
                    order.Add(slot);
                }

                done.Signal();
            });
        }

        Assert.True(await Task.Run(() => done.Wait(TimeSpan.FromSeconds(5))));
        Assert.Equal([1, 2, 0], order);

        // Within 2 ms on an idle host; the bound leaves room for a loaded test runner while staying under half a 50 ms room step.
        for (var i = 0; i < due.Length; i++) {
            Assert.InRange(woke[i], due[i], due[i] + 20);
        }
    }

    [Fact]
    public void AWakeUpThatThrowsDoesNotStopTheClock()
    {
        using var clock = new WiredWakeClock();
        using var woke = new ManualResetEventSlim();

        clock.After(5, () => throw new InvalidOperationException());
        clock.After(10, woke.Set);

        Assert.True(woke.Wait(TimeSpan.FromSeconds(2)));
    }
}
