namespace Plus.HabboHotel.Items.Wired.Modern.Addons;

public sealed class WiredRandomActionPicker(int amount, int skipExecutions, Random random) : IWiredActionPicker
{
    private readonly Queue<uint[]> _recent = new();

    public IReadOnlyList<uint> Pick(IReadOnlyList<uint> actionIds)
    {
        if (actionIds.Count == 0) {
            return [];
        }

        var candidates = actionIds.Distinct().ToArray();
        random.Shuffle(candidates);
        var count = Math.Min(Math.Clamp(amount, 1, 1000), candidates.Length);
        var history = _recent.ToArray();
        var result = new List<uint>(count);

        // Observed on AIR WIN63-202610131318-860371703: the add-on avoids the effects of the last N executions of its stack. When too few
        // are left it stops avoiding the OLDEST executions first (pick 1 / avoid 2 over two effects strictly alternates), so the top-up is
        // least-recently-used, never a random repeat.
        for (var oldest = 0; oldest <= history.Length && result.Count < count; oldest++) {
            var avoided = history.Skip(oldest).SelectMany(x => x).ToHashSet();
            result.AddRange(candidates.Where(x => !avoided.Contains(x) && !result.Contains(x)).Take(count - result.Count));
        }

        if (skipExecutions > 0) {
            _recent.Enqueue(result.ToArray());

            while (_recent.Count > Math.Clamp(skipExecutions, 0, 1000)) {
                _recent.Dequeue();
            }
        }

        return result;
    }

    public void Reset() => _recent.Clear();
}

public sealed class WiredUnseenActionPicker : IWiredActionPicker
{
    private readonly HashSet<uint> _seen = [];
    private readonly Queue<uint> _insertionOrder = new();

    public IReadOnlyList<uint> Pick(IReadOnlyList<uint> actionIds)
    {
        if (actionIds.Count == 0) {
            return [];
        }

        var unseen = actionIds.Where(x => !_seen.Contains(x)).Take(1).ToArray();

        if (unseen.Length == 0) {
            Reset();
            unseen = [actionIds[0]];
        }

        if (_seen.Count >= 1000) {
            _seen.Remove(_insertionOrder.Dequeue());
        }

        _seen.Add(unseen[0]);
        _insertionOrder.Enqueue(unseen[0]);

        return unseen;
    }

    public void Reset()
    {
        _seen.Clear();
        _insertionOrder.Clear();
    }
}
