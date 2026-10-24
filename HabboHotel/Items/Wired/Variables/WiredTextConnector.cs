using System.Collections;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// A variable's Text Connector: the label of each number and the fallback text of a number with no label. The fallback is saved as a "_=text" line and replaces an unlabelled number in text mode.
/// </summary>
public sealed class WiredTextConnector(IReadOnlyDictionary<long, string> labels, string? fallback) : IReadOnlyDictionary<long, string>
{
    public string? Fallback { get; } = fallback;

    /// <summary>The catalog lists a key as a 32 bit int: a key outside that range is sent as int.MinValue or int.MaxValue. The lines stay in the order of the real key.</summary>
    public static ImmutableArray<KeyValuePair<int, string>> ToWire(IEnumerable<KeyValuePair<long, string>> labels) =>
        [.. labels.OrderBy(pair => pair.Key).Select(pair => new KeyValuePair<int, string>((int)Math.Clamp(pair.Key, int.MinValue, int.MaxValue), pair.Value))];

    public IEnumerable<long> Keys => labels.Keys;
    public IEnumerable<string> Values => labels.Values;
    public int Count => labels.Count;
    public string this[long key] => labels[key];
    public bool ContainsKey(long key) => labels.ContainsKey(key);
    public bool TryGetValue(long key, out string value) => labels.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<long, string>> GetEnumerator() => labels.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
