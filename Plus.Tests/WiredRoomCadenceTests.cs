using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public class WiredRoomCadenceTests
{
    [Fact]
    public async Task FastAndFullPassShareOneTaskAndDoNotOverlap()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(RoomCycle.TryStart(room, () => { started.SetResult(); release.Task.GetAwaiter().GetResult(); }));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var originalTask = room.ProcessTask;
        Assert.False(RoomCycle.TryStart(room, () => throw new InvalidOperationException("overlap")));
        Assert.Same(originalTask, room.ProcessTask);
        Assert.Equal(0, room.IsLagging);
        release.SetResult();
        await originalTask.WaitAsync(TimeSpan.FromSeconds(5));
        var calls = 0;
        Assert.True(RoomCycle.TryStart(room, () => calls++));
        await room.ProcessTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, calls);
        room.ProcessTask.Dispose();
    }

    [Fact]
    public async Task TwoThreadsStartingPassesNeverRunTwoAtOnce()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var running = 0;
        var overlaps = 0;
        var passes = 0;
        var stop = DateTime.UtcNow.AddSeconds(1);

        void Pass()
        {
            if (Interlocked.Increment(ref running) > 1) {
                Interlocked.Increment(ref overlaps);
            }

            Thread.SpinWait(200);
            Interlocked.Increment(ref passes);
            Interlocked.Decrement(ref running);
        }

        void Hammer(bool full)
        {
            while (DateTime.UtcNow < stop) {
                RoomCycle.TryStart(room, Pass, full);
            }
        }

        await Task.WhenAll(Task.Factory.StartNew(() => Hammer(false), TaskCreationOptions.LongRunning),
            Task.Factory.StartNew(() => Hammer(true), TaskCreationOptions.LongRunning));
        await room.ProcessTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(passes > 0);
        Assert.Equal(0, overlaps);
    }

    [Fact]
    public async Task AWiredPassHoldingTheSlotIsNotAMissedFullTick()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(RoomCycle.TryStart(room, () => release.Task.GetAwaiter().GetResult()));

        Assert.False(RoomCycle.FullPassRunning(room));
        Assert.True(RoomCycle.Next(RoomCycle.FullPassRunning(room), RoomCycle.MissesUntilCrash - 1).Start);
        Assert.False(RoomCycle.TryStart(room, () => { }, full: true));

        release.SetResult();
        await room.ProcessTask.WaitAsync(TimeSpan.FromSeconds(5));

        var fullRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(RoomCycle.TryStart(room, () => fullRelease.Task.GetAwaiter().GetResult(), full: true));
        Assert.True(RoomCycle.FullPassRunning(room));
        Assert.True(RoomCycle.Next(RoomCycle.FullPassRunning(room), RoomCycle.MissesUntilCrash - 1).Crashed);

        fullRelease.SetResult();
        await room.ProcessTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AnInlinePassRunsOnTheCallingThreadAndFreesTheSlot()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var caller = Environment.CurrentManagedThreadId;
        int? ranOn = null;

        Assert.True(RoomCycle.TryStart(room, () => ranOn = Environment.CurrentManagedThreadId, inline: true));

        Assert.Equal(caller, ranOn);
        Assert.True(RoomCycle.TryStart(room, () => { }, inline: true));
    }

    [Fact]
    public void FastCadenceDoesNotChangeLegacyTickOrCrashDuration()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(50), RoomCycle.WiredInterval);
        Assert.Equal(TimeSpan.FromMilliseconds(500), RoomCycle.Interval);
        Assert.Equal(TimeSpan.FromSeconds(15), RoomCycle.Interval * RoomCycle.MissesUntilCrash);
        var last = DateTimeOffset.UnixEpoch;
        Assert.False(RoomCycle.IsDue(last, last.AddMilliseconds(50)));
        Assert.True(RoomCycle.IsDue(last, last.AddMilliseconds(500)));
    }

    [Fact]
    public void LatePassesStayOnTheWiredGrid()
    {
        // Each pass starts a little late; recording the boundary keeps the next one on the 500 ms grid of the Wired clock.
        var start = DateTimeOffset.UnixEpoch.AddSeconds(100);
        var last = RoomCycle.Boundary(start.AddMilliseconds(7), 1007);
        Assert.Equal(start, last);
        Assert.False(RoomCycle.IsDue(last, start.AddMilliseconds(499)));
        var late = start.AddMilliseconds(504);
        Assert.True(RoomCycle.IsDue(last, late));
        Assert.Equal(start.AddMilliseconds(500), RoomCycle.Boundary(late, 1504));
    }
}
