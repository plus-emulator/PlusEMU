using Plus.HabboHotel.Rooms.Instance;

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
    /// so the room ticks stay on that grid instead of drifting by each pass's lateness. A walk's UserUpdates keep one phase within a few ms, and Wired steps land on those ticks.
    /// </summary>
    public static DateTimeOffset Boundary(DateTimeOffset now, long monotonicMilliseconds) => Boundary(now, monotonicMilliseconds, Interval);

    /// <summary>The start of the <paramref name="interval"/> step of that grid at or before <paramref name="now"/>.</summary>
    public static DateTimeOffset Boundary(DateTimeOffset now, long monotonicMilliseconds, TimeSpan interval) =>
        now - TimeSpan.FromMilliseconds(monotonicMilliseconds % (long)interval.TotalMilliseconds);

    private const int Idle = 0;
    private const int FastPass = 1;
    private const int FullPass = 2;

    // Both passes share the room's task slot, claimed atomically: the room loop and the wired wake clock start passes from different
    // threads. Fast misses do not change legacy lag counters.
    /// <param name="workers">Run on these pre-started threads, for a pass that must start at a set time: a pool item waits behind a busy
    /// pool and a new thread takes as long to start on a loaded server.</param>
    public static bool TryStart(Room room, Action pass, bool full = false, WiredWakeWorkers? workers = null, double startAtMs = 0)
    {
        if (room.IsCrashed || room.MDisposed || Interlocked.CompareExchange(ref room.CycleSlot, full ? FullPass : FastPass, Idle) != Idle) {
            return false;
        }

        try {
            void Run()
            {
                try {
                    pass();
                }
                finally {
                    Volatile.Write(ref room.CycleSlot, Idle);
                }
            }

            if (workers != null) {
                var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                room.ProcessTask = done.Task;
                workers.Enqueue(() =>
                {
                    try {
                        Run();
                    }
                    finally {
                        done.TrySetResult();
                    }
                }, startAtMs);
            }
            else {
                room.ProcessTask = Task.Run(Run);
            }
        }
        catch {
            Volatile.Write(ref room.CycleSlot, Idle);
            throw;
        }

        return true;
    }

    /// <summary>Whether the room's slot is held by a full pass; a fast or wake-started wired pass holding it is not a missed tick.</summary>
    public static bool FullPassRunning(Room room) => Volatile.Read(ref room.CycleSlot) == FullPass;

    public static RoomTick Next(bool workInProgress, int lag)
    {
        if (!workInProgress) {
            return new(true, 0, false);
        }

        var nextLag = lag + 1;

        return new(false, nextLag, nextLag >= MissesUntilCrash);
    }
}
