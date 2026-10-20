using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using Dapper;
using Plus.Database;

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

    /// <summary>The categories an Import add-on has on (its text connector, subvariable creator and variable FX flags).</summary>
    public WiredSharedAddons Pick(bool textConnector, bool subvariableCreators, bool variableFx) => new()
    {
        TextConnector = textConnector ? TextConnector : [],
        Level = subvariableCreators ? Level : null,
        Fx = variableFx ? Fx : []
    };

    public string ToJson() => JsonSerializer.Serialize(new Stored([.. TextConnector.Select(pair => new StoredPair(pair.Key, pair.Value))], Level,
        [.. Fx.Select(fx => new StoredFx(fx.Box, [.. fx.Ints], [.. fx.VariableIds], fx.Text))]));

    public static WiredSharedAddons? FromJson(string json)
    {
        try {
            var stored = JsonSerializer.Deserialize<Stored>(json);

            return stored is null ? null : new()
            {
                TextConnector = [.. (stored.TextConnector ?? []).Select(pair => new KeyValuePair<int, string>(pair.Key, pair.Value))],
                Level = stored.Level,
                Fx = [.. (stored.Fx ?? []).Select(fx => new WiredSharedFx(fx.Box, [.. fx.Ints], [.. fx.VariableIds], fx.Text))]
            };
        }
        catch (JsonException) {
            return null;
        }
    }

    private sealed record StoredPair(int Key, string Value);
    private sealed record StoredFx(string Box, int[] Ints, string[] VariableIds, string Text);
    private sealed record Stored(StoredPair[]? TextConnector, string? Level, StoredFx[]? Fx);
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

/// <summary>Keeps what Share add-ons published in the database, so a reference has it again after a restart or in an unloaded room.</summary>
public sealed class DatabaseWiredSharedAddonStore(IDatabase database) : IWiredSharedAddonStore
{
    public WiredSharedAddons? Load(uint referenceItemId)
    {
        using var connection = database.Connection();
        var json = connection.QuerySingleOrDefault<string>("SELECT addons FROM wired_shared_variable_addons WHERE reference_item_id=@referenceItemId", new { referenceItemId });

        return json is null ? null : WiredSharedAddons.FromJson(json);
    }

    public void Save(uint referenceItemId, WiredSharedAddons? addons)
    {
        using var connection = database.Connection();

        if (addons is null or { IsEmpty: true }) {
            connection.Execute("DELETE FROM wired_shared_variable_addons WHERE reference_item_id=@referenceItemId", new { referenceItemId });

            return;
        }

        connection.Execute("""
            INSERT INTO wired_shared_variable_addons (reference_item_id,addons) VALUES (@referenceItemId,@json)
            ON DUPLICATE KEY UPDATE addons=VALUES(addons)
            """, new { referenceItemId, json = addons.ToJson() });
    }
}
