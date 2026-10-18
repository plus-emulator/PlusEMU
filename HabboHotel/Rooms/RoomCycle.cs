namespace Plus.HabboHotel.Rooms;

internal static class RoomCycle
{
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan WiredInterval = TimeSpan.FromMilliseconds(50);
    public const int MissesUntilCrash = 30;

    /// <summary>How close a wired wake-up may be for the 50 ms step to leave the room to it.</summary>
    public const long WakePriorityMs = 25;

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

    private const int Idle = 0;
    private const int FastPass = 1;
    private const int FullPass = 2;

    // Both passes share the room's task slot, claimed atomically: the room loop and the wired wake clock start passes from different
    // threads. Fast misses do not change legacy lag counters.
    /// <param name="inline">Run on the calling thread, for a pass that must start at a set time: a pool item waits behind a busy pool and
    /// a new thread takes as long to start on a loaded server.</param>
    public static bool TryStart(Room room, Action pass, bool full = false, bool inline = false)
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

            if (inline) {
                Run();
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
