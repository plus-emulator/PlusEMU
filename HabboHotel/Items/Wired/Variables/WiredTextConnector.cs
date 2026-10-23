using System.Collections;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// A variable's Text Connector: the label of each number and the fallback text of a number with no label. The fallback is saved as a "_=text" line and replaces an unlabelled number in text mode.
/// </summary>
public sealed class WiredTextConnector(IReadOnlyDictionary<int, string> labels, string? fallback) : IReadOnlyDictionary<int, string>
{
    public string? Fallback { get; } = fallback;
    public IEnumerable<int> Keys => labels.Keys;
    public IEnumerable<string> Values => labels.Values;
    public int Count => labels.Count;
    public string this[int key] => labels[key];
    public bool ContainsKey(int key) => labels.ContainsKey(key);
    public bool TryGetValue(int key, out string value) => labels.TryGetValue(key, out value!);
    public IEnumerator<KeyValuePair<int, string>> GetEnumerator() => labels.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
