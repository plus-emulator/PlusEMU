using System.Diagnostics;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Wakes rooms at the time a delayed effect falls due. An effect with a delay of N pulses lands N x 500 ms after its firing, within 2 ms.
/// Pool timers drift several ms under load, so one dedicated thread sleeps until just before the earliest due time and yields through the last stretch.
/// </summary>
public sealed class WiredWakeClock : IDisposable
{
    private const double YieldWindowMs = 2;

    private readonly object _gate = new();
    private readonly PriorityQueue<Action, double> _queue = new();
    private readonly Thread _thread;
    private bool _stopped;

    public WiredWakeClock()
    {
        _thread = new(Run) { IsBackground = true, Name = "Wired wake clock", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
    }

    public static WiredWakeClock Shared { get; } = new();

    public static double Now => Stopwatch.GetElapsedTime(0).TotalMilliseconds;

    public void After(double delayMs, Action wake) => At(Now + delayMs, wake);

    public void At(double dueMs, Action wake)
    {
        lock (_gate) {
            _queue.Enqueue(wake, dueMs);
            Monitor.Pulse(_gate);
        }
    }

    public void Dispose()
    {
        lock (_gate) {
            _stopped = true;
            Monitor.Pulse(_gate);
        }
    }

    private void Run()
    {
        while (true) {
            Action? wake = null;

            lock (_gate) {
                if (_stopped) {
                    return;
                }

                if (!_queue.TryPeek(out _, out var dueMs)) {
                    Monitor.Wait(_gate);
                    continue;
                }

                var left = dueMs - Now;

                if (left > YieldWindowMs) {
                    Monitor.Wait(_gate, TimeSpan.FromMilliseconds(left - YieldWindowMs));
                    continue;
                }

                if (left <= 0) {
                    wake = _queue.Dequeue();
                }
            }

            if (wake is null) {
                Thread.Yield();
                continue;
            }

            try {
                wake();
            }
            catch {
                // A room's wake-up must not stop the clock for the others.
            }
        }
    }
}
