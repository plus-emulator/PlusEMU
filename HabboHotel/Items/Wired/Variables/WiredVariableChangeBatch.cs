namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>One firing's arithmetic, ordered by operation and written once per variable holder.</summary>
public sealed class WiredVariableChangeBatch
{
    private readonly List<Change> _changes = [];
    private readonly List<Written> _written = [];
    private sealed record Change(WiredVariableModule Module, WiredVariableReference Reference, WiredVariableHolder Holder,
        int Operation, long Operand, WiredVariableFrame Frame);
    private sealed record Written(WiredVariableModule Module, WiredVariableReference Destination, Action<bool> Callback);

    public bool IsEmpty => _changes.Count == 0 && _written.Count == 0;
    public void Add(WiredVariableModule module, WiredVariableReference reference, WiredVariableHolder holder,
        int operation, long operand, WiredVariableFrame frame) => _changes.Add(new(module, reference, holder, operation, operand, frame));

    /// <summary>Runs once the batch is written, with whether the write moved the destination's value on any holder.</summary>
    public void AfterFlush(WiredVariableModule module, WiredVariableReference destination, Action<bool> written) =>
        _written.Add(new(module, destination, written));

    public bool Flush()
    {
        var changes = _changes.ToArray();
        var written = _written.ToArray();
        _changes.Clear();
        _written.Clear();
        var changed = false;
        var moved = new HashSet<(WiredVariableModule, WiredVariableReference)>();

        foreach (var group in changes.GroupBy(change => (change.Module, Reference: change.Module.WriteTarget(change.Reference), change.Holder))) {
            var first = group.First();

            if (first.Module.Read(first.Reference, first.Holder, first.Frame) is null) {
                continue;
            }

            var ordered = group.OrderBy(change => Rank(change.Operation)).ToArray();
            var before = first.Module.ValueChanges;
            changed |= first.Module.Change(first.Reference, first.Holder, WiredVariableMutation.Set, current =>
                ordered.Aggregate(current, (value, change) => WiredVariableArithmetic.Apply(change.Operation, value, change.Operand)), first.Frame, notifyUnchanged: true);

            if (first.Module.ValueChanges != before) {
                moved.Add((group.Key.Module, group.Key.Reference));
            }
        }

        foreach (var write in written) {
            write.Callback(moved.Contains((write.Module, write.Module.WriteTarget(write.Destination))));
        }

        return changed;
    }

    public void Clear()
    {
        _changes.Clear();
        _written.Clear();
    }

    private static int Rank(int operation) => operation switch
    {
        0 => 0,
        5 => 1,
        3 => 2,
        4 => 3,
        6 => 4,
        1 => 5,
        2 => 6,
        _ => 7
    };
}
