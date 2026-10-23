namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// Where a room log line came from, as the monitor rows name it. Plus only produces these five: the engine has no heavy,
/// overload or kill state. The log page itself knows two sources, <see cref="WiredRoomLogEntry.WireSource"/>.
/// </summary>
public enum WiredLogSource
{
    // Habbo numbers PLACEMENT_FAILURE 8 too; 8 is already this log's own WiredLog row, so it takes the next free value.
    ExecutionCap = 0, DelayedEventsCap = 1, RecursionTimeout = 5, WiredLog = 8, PlacementFailure = 9, TransactionFailure = 10
}

public sealed record WiredRoomLogEntry(long Id, int Level, WiredLogSource Source, uint BoxId, string Label, string Reason, DateTimeOffset Timestamp)
{
    public static string TypeName(WiredLogSource source) => source switch
    {
        WiredLogSource.ExecutionCap => "EXECUTION_CAP",
        WiredLogSource.DelayedEventsCap => "DELAYED_EVENTS_CAP",
        WiredLogSource.RecursionTimeout => "RECURSION_TIMEOUT",
        WiredLogSource.PlacementFailure => "PLACEMENT_FAILURE",
        WiredLogSource.TransactionFailure => "TRANSACTION_FAILURE",
        _ => "WIRED_LOG"
    };
    // The client's log levels (default texts wiredmenu.logs_overview.log_level.N): 0 INFO, 1 WARN, 2 ERROR, 3 DEBUG.
    public static string LevelName(int level) => level switch { 0 => "INFO", 1 => "WARNING", 2 => "ERROR", _ => "DEBUG" };

    // A Write to Logs line is listed under source 1. The client's two log sources are 0 System (the engine's own notes)
    // and 1 Wired (lines the boxes wrote).
    public const int SystemSource = 0;
    public const int WiredSource = 1;
    public int WireSource => Source == WiredLogSource.WiredLog ? WiredSource : SystemSource;
    // A "write to logs" line reads as the box wrote it; an engine note names its limit.
    // A transaction failure line is written whole, as Habbo words it.
    public string Message => Source is WiredLogSource.WiredLog or WiredLogSource.TransactionFailure ? Reason : $"{TypeName(Source)}: {Reason}";
}

public sealed record WiredRoomLogPage(int Total, int Page, int Amount, IReadOnlyList<WiredRoomLogEntry> Entries);
/// <summary>Every occurrence of one source since the log was last cleared, including lines the buffer has dropped.</summary>
public sealed record WiredRoomLogTally(WiredLogSource Source, int Count, WiredRoomLogEntry? Latest);
public sealed record WiredRoomLogSummary(IReadOnlyList<WiredRoomLogTally> Tallies, IReadOnlyList<WiredRoomLogEntry> Recent);

/// <summary>
/// Room-owned bounded log. The log page and the monitor read this same buffer. The client states that only the most recent
/// 200 logs are stored per type, so the capacity bounds each of the two log sources on its own.
/// </summary>
public sealed class WiredRoomLog(int capacity = 200)
{
    public const int InfoLevel = 0;
    public const int ErrorLevel = 2;
    public const int DebugLevel = 3;
    private static readonly WiredLogSource[] Sources =
        [WiredLogSource.ExecutionCap, WiredLogSource.DelayedEventsCap, WiredLogSource.RecursionTimeout, WiredLogSource.WiredLog,
            WiredLogSource.PlacementFailure];
    private readonly List<WiredRoomLogEntry> _entries = [];
    private readonly Dictionary<WiredLogSource, (int Count, WiredRoomLogEntry Latest)> _tallies = [];
    private long _nextId;
    private readonly object _gate = new();

    public void Append(int level, uint boxId, string message, DateTimeOffset timestamp) =>
        Append(level, WiredLogSource.WiredLog, boxId, "", message, timestamp);

    public void Append(int level, WiredLogSource source, uint boxId, string label, string reason, DateTimeOffset timestamp)
    {
        lock (_gate) {
            var entry = new WiredRoomLogEntry(++_nextId, level, source, boxId, label, reason, timestamp);
            _entries.Add(entry);

            if (_entries.Count(x => x.WireSource == entry.WireSource) > Math.Max(1, capacity)) {
                _entries.RemoveAt(_entries.FindIndex(x => x.WireSource == entry.WireSource));
            }

            _tallies[source] = (_tallies.GetValueOrDefault(source).Count + 1, entry);
        }
    }

    public WiredRoomLogPage Read(int page, int amount, int level = -1, string query = "", int source = -1)
    {
        page = Math.Max(0, page);
        amount = Math.Clamp(amount, 1, 100);

        lock (_gate) {
            var matching = _entries.AsEnumerable().Reverse().Where(entry => (level < 0 || entry.Level == level)
                && (source < 0 || entry.WireSource == source)
                && (query.Length == 0 || entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
            // A page past the end, after a clear or a narrower filter, reads as the last page.
            page = Math.Min(page, Math.Max(0, (matching.Length - 1) / amount));

            return new(matching.Length, page, amount, matching.Skip(page * amount).Take(amount).ToArray());
        }
    }

    public WiredRoomLogSummary Summarize(int recent)
    {
        lock (_gate) {
            return new(Sources.Select(source => _tallies.TryGetValue(source, out var tally)
                    ? new WiredRoomLogTally(source, tally.Count, tally.Latest) : new(source, 0, null)).ToArray(),
                _entries.AsEnumerable().Reverse().Take(Math.Max(0, recent)).ToArray());
        }
    }

    public void Clear()
    {
        lock (_gate) {
            _entries.Clear();
            _tallies.Clear();
        }
    }
}
