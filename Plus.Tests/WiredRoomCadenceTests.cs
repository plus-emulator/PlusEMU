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
