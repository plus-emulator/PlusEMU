namespace Plus.HabboHotel.Rooms;

internal static class RoomCycle
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan WiredInterval = TimeSpan.FromMilliseconds(50);
    public const int MissesUntilCrash = 30;

    public readonly record struct RoomTick(bool Start, int Lag, bool Crashed);

    public static bool IsDue(DateTimeOffset lastRun, DateTimeOffset now) =>
        now - lastRun >= Interval;

    /// <summary>
    /// The 500 ms boundary at or before <paramref name="now"/> on the monotonic grid the Wired engine counts in (its timers, flashes and
    /// scheduled steps fall on multiples of 500 of <paramref name="monotonicMilliseconds"/>). A pass records this instead of its own start,
    /// so the room ticks stay on that grid instead of drifting by each pass's lateness. Observed on hhus (AIR WIN63-202610131318-860371703):
    /// a walk's UserUpdates keep one phase within a few ms, and Wired steps land on those ticks; ours moved about 4 ms a tick.
    /// </summary>
    public static DateTimeOffset Boundary(DateTimeOffset now, long monotonicMilliseconds) =>
        now - TimeSpan.FromMilliseconds(monotonicMilliseconds % (long)Interval.TotalMilliseconds);

    // Both passes share the room's task slot. Fast misses do not change legacy lag counters.
    public static bool TryStart(Room room, Action pass)
    {
        if (room.IsCrashed || room.MDisposed || room.ProcessTask is { IsCompleted: false }) {
            return false;
        }

        room.ProcessTask?.Dispose();
        room.ProcessTask = Task.Run(pass);

        return true;
    }

    public static RoomTick Next(bool workInProgress, int lag)
    {
        if (!workInProgress) {
            return new(true, 0, false);
        }

        var nextLag = lag + 1;

        return new(false, nextLag, nextLag >= MissesUntilCrash);
    }
}
