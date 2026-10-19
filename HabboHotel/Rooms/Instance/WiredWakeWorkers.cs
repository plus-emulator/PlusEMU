using System.Collections.Concurrent;

namespace Plus.HabboHotel.Rooms.Instance;

/// <summary>
/// Pre-started threads that run wake-up passes. A queued pool item, or a newly created thread, can wait several ms to start on a busy
/// server; these threads are already waiting. Several of them keep one long pass from holding back every other room's wake-ups, and
/// the wake clock itself only hands work over.
/// </summary>
public sealed class WiredWakeWorkers : IDisposable
{
    private readonly BlockingCollection<(Action Work, double StartAt)> _work = new(new ConcurrentQueue<(Action, double)>());
    private readonly Thread[] _threads;

    public WiredWakeWorkers(int count)
    {
        _threads = Enumerable.Range(0, Math.Max(1, count)).Select(index =>
        {
            var thread = new Thread(Run) { IsBackground = true, Name = $"Wired wake worker {index}", Priority = ThreadPriority.AboveNormal };
            thread.Start();

            return thread;
        }).ToArray();
    }

    public static WiredWakeWorkers Shared { get; } = new(Math.Max(2, Environment.ProcessorCount / 2));

    public int Count => _threads.Length;

    /// <summary>
    /// Queues a pass. With <paramref name="startAtMs"/> (on <see cref="WiredWakeClock.Now"/>) the worker holds it until then: the clock
    /// hands it over a few ms early, so the worker's own wake-up latency is spent before the due time instead of after it.
    /// </summary>
    public void Enqueue(Action work, double startAtMs = 0) => _work.Add((work, startAtMs));

    public void Dispose() => _work.CompleteAdding();

    private void Run()
    {
        foreach (var (work, startAt) in _work.GetConsumingEnumerable()) {
            while (WiredWakeClock.Now < startAt) {
                Thread.Yield();
            }

            try {
                work();
            }
            catch {
                // A pass that throws must not stop the worker for the other rooms.
            }
        }
    }
}
