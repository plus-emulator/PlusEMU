using System.Reflection;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void AWakeUpHoldsItsWorkerOnlyAFewMsWhenTheEngineClockIsBehind()
    {
        using var f = new TeleportFixture();
        using var workers = new WiredWakeWorkers(1);
        var wired = f.Room.GetWired();
        wired.Workers = workers;
        using var next = new ManualResetEventSlim();

        typeof(WiredComponent).GetMethod("Wake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(wired, [5000L]);
        workers.Enqueue(next.Set);

        Assert.True(next.Wait(1000), "A wake-up held its worker until a due time on another clock.");
    }
}
