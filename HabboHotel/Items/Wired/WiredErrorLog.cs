namespace Plus.HabboHotel.Items.Wired;

/// <summary>The monitor's error rows, in the order the room sends them. Two rows share error id 8.</summary>
public enum WiredErrorKind
{
    ExecutionCap, DelayedEventsCap, ExecutorOverload, MarkedAsHeavy, Killed, RecursionTimeout, TooManyVariables,
    TransactionFailure, PlacementFailure, VariableFxLimit
}

/// <summary>One monitor error row. A row that never happened has count 0 and -1 ms since its last occurrence.</summary>
public sealed record WiredErrorRow(int ErrorId, string ErrorName, string Category, int ThrowCount, long MsSinceLastOccurrence);

/// <summary>Room-owned counters behind the monitor's error rows. Clearing resets every row but keeps all ten.</summary>
public sealed class WiredErrorLog(TimeProvider clock)
{
    // Observed on AIR WIN63-202610131318-860371703: exactly these ten rows in this order, every one ERROR except
    // MARKED_AS_HEAVY (WARNING), and still all ten (0 / -1) after a clear.
    private static readonly (int Id, string Name, string Category)[] Rows =
    [
        (0, "EXECUTION_CAP", "ERROR"), (1, "DELAYED_EVENTS_CAP", "ERROR"), (2, "EXECUTOR_OVERLOAD", "ERROR"),
        (3, "MARKED_AS_HEAVY", "WARNING"), (4, "KILLED", "ERROR"), (5, "RECURSION_TIMEOUT", "ERROR"),
        (6, "TOO_MANY_VARIABLES", "ERROR"), (7, "TRANSACTION_FAILURE", "ERROR"), (8, "PLACEMENT_FAILURE", "ERROR"),
        (8, "VARIABLE_FX_LIMIT", "ERROR")
    ];

    private readonly object _gate = new();
    private readonly int[] _counts = new int[Rows.Length];
    private readonly long?[] _last = new long?[Rows.Length];

    public void Record(WiredErrorKind kind)
    {
        var index = (int)kind;

        if ((uint)index >= Rows.Length) {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        lock (_gate) {
            _counts[index] = _counts[index] == int.MaxValue ? int.MaxValue : _counts[index] + 1;
            _last[index] = clock.GetTimestamp();
        }
    }

    public IReadOnlyList<WiredErrorRow> Read()
    {
        var now = clock.GetTimestamp();

        lock (_gate) {
            return Rows.Select((row, index) => new WiredErrorRow(row.Id, row.Name, row.Category, _counts[index],
                _last[index] is { } last ? (long)clock.GetElapsedTime(last, now).TotalMilliseconds : -1)).ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate) {
            Array.Clear(_counts);
            Array.Clear(_last);
        }
    }
}
