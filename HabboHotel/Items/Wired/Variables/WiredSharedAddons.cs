using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>One Variable FX add-on a Share add-on publishes: its box name and the settings of its editor.</summary>
public sealed record WiredSharedFx(string Box, ImmutableArray<int> Ints, ImmutableArray<string> VariableIds, string Text);

/// <summary>
/// What a Share add-on has published about one shared variable: its text connector, its level system (the configuration text of the
/// Level-up System add-on) and its Variable FX. A reference to that variable in another room receives it when the Share add-on is
/// synchronized, and its Import add-on picks the categories it shows.
/// </summary>
public sealed record WiredSharedAddons
{
    public ImmutableArray<KeyValuePair<int, string>> TextConnector { get; init; } = [];
    public string? Level { get; init; }
    public ImmutableArray<WiredSharedFx> Fx { get; init; } = [];
    public bool IsEmpty => TextConnector.IsEmpty && Level is null && Fx.IsEmpty;
    public string ToJson() => throw new NotImplementedException();
    public static WiredSharedAddons? FromJson(string json) => throw new NotImplementedException();
}

/// <summary>The published add-ons each reference variable has received, by the item id of the reference.</summary>
public interface IWiredSharedAddonStore
{
    WiredSharedAddons? Load(uint referenceItemId);

    /// <summary>Replaces what a reference received; null or an empty set takes it all away.</summary>
    void Save(uint referenceItemId, WiredSharedAddons? addons);
}

public sealed class MemoryWiredSharedAddonStore : IWiredSharedAddonStore
{
    private readonly ConcurrentDictionary<uint, WiredSharedAddons> _addons = new();
    public WiredSharedAddons? Load(uint referenceItemId) => _addons.GetValueOrDefault(referenceItemId);
    public void Save(uint referenceItemId, WiredSharedAddons? addons)
    {
        if (addons is null or { IsEmpty: true }) {
            _addons.TryRemove(referenceItemId, out _);
        }
        else {
            _addons[referenceItemId] = addons;
        }
    }
}
