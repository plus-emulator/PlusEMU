using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Local native-wire capabilities, independent of scalar menu descriptions.</summary>
public sealed record WiredNativeVariable(string Id, int Type, string Name, int Availability, int Target,
    bool AlwaysAvailable, bool CanCreateAndDelete, bool HasValue, bool CanWriteValue,
    bool CanInterceptChanges, bool IsInvisible, bool CanReadCreationTime, bool CanReadLastUpdateTime,
    ImmutableArray<KeyValuePair<int, string>>? Connector)
{
    public bool RequiresExecutionContext { get; init; }
    public string? TextConnectorFallback { get; init; }
    public bool HasTextConnectorFallback => Connector.HasValue && TextConnectorFallback is not null;

    /// <summary>The AIR variable record: the catalog diff leads it with the row hash, a shared-variable row does not.</summary>
    public void Write(Plus.HabboHotel.GameClients.IOutgoingPacket packet, bool withHash)
    {
        if (withHash) {
            packet.WriteInteger(Hash);
        }

        packet.WriteString(Id);
        packet.WriteInteger(Type);
        packet.WriteString(Name);
        packet.WriteInteger(Availability);
        packet.WriteInteger(Target);
        packet.WriteBoolean(AlwaysAvailable);
        packet.WriteBoolean(CanCreateAndDelete);
        packet.WriteBoolean(HasValue);
        packet.WriteBoolean(CanWriteValue);
        packet.WriteBoolean(CanInterceptChanges);
        packet.WriteBoolean(IsInvisible);
        packet.WriteBoolean(RequiresExecutionContext);
        packet.WriteBoolean(CanReadCreationTime);
        packet.WriteBoolean(CanReadLastUpdateTime);
        packet.WriteBoolean(Connector.HasValue);

        if (Connector is { } connector) {
            packet.WriteInteger(connector.Length);

            foreach (var pair in connector) {
                packet.WriteInteger(pair.Key);
                packet.WriteString(pair.Value);
            }

            packet.WriteBoolean(HasTextConnectorFallback);

            if (HasTextConnectorFallback) {
                packet.WriteString(TextConnectorFallback!);
            }
        }
    }

    public int Hash
    {
        get
        {
            var hash = TextHash(Id);

            foreach (var value in new[] { Type, TextHash(Name), Availability, Target, AlwaysAvailable ? 1 : 0,
                CanCreateAndDelete ? 1 : 0, HasValue ? 1 : 0, CanWriteValue ? 1 : 0, CanInterceptChanges ? 1 : 0,
                IsInvisible ? 1 : 0, RequiresExecutionContext ? 1 : 0, CanReadCreationTime ? 1 : 0,
                CanReadLastUpdateTime ? 1 : 0, Connector.HasValue ? 1 : 0 }) {
                hash = unchecked(hash * 31 + value);
            }

            if (Connector is { } connector) {
                hash = unchecked(hash * 31 + connector.Length);

                foreach (var pair in connector) {
                    hash = unchecked(hash * 31 + pair.Key);
                    hash = unchecked(hash * 31 + TextHash(pair.Value));
                }

                hash = unchecked(hash * 31 + (HasTextConnectorFallback ? 1 : 0));

                if (HasTextConnectorFallback) {
                    hash = unchecked(hash * 31 + TextHash(TextConnectorFallback!));
                }
            }

            return hash;
        }
    }

    private static int TextHash(string text)
    {
        var hash = 0;

        foreach (var character in text) {
            hash = unchecked(hash * 31 + character);
        }

        return hash;
    }
}

public sealed record WiredNativeCatalogDiff(int Hash, bool LastChunk, ImmutableArray<string> Removed,
    ImmutableArray<WiredNativeVariable> Changed);

/// <summary>Frozen rows are the sole source of both catalog hashes and response bodies.</summary>
public sealed class WiredNativeCatalog(ImmutableArray<WiredNativeVariable> variables)
{
    public ImmutableArray<WiredNativeVariable> Variables { get; } = variables;
    public int Hash => Variables.Aggregate(Variables.Length, (hash, variable) => unchecked(hash * 31 + variable.Hash));

    public ImmutableArray<WiredNativeCatalogDiff> Diff(IReadOnlyDictionary<string, int> known)
    {
        var current = Variables.ToDictionary(variable => variable.Id, StringComparer.Ordinal);
        var removed = known.Keys.Where(id => !current.ContainsKey(id)).Order(StringComparer.Ordinal).ToImmutableArray();
        var changed = Variables.Where(variable => !known.TryGetValue(variable.Id, out var hash) || hash != variable.Hash).ToArray();
        var count = Math.Max(1, (changed.Length + 99) / 100);

        return Enumerable.Range(0, count).Select(index => new WiredNativeCatalogDiff(Hash, index == count - 1,
            index == 0 ? removed : [], changed.Skip(index * 100).Take(100).ToImmutableArray())).ToImmutableArray();
    }
}

/// <summary>One shared-variable row of the editor's reference list: the room it lives in and the variable block.</summary>
public sealed record WiredNativeSharedVariable(uint RoomId, string RoomName, WiredNativeVariable Variable)
{
    /// <summary>
    /// A shared source offered from another room: the capabilities of its own ordinary domain (a user variable can be created and
    /// deleted, a room variable cannot; writes need a value). Whether this room's reference is read-only is the reference's own choice.
    /// </summary>
    public static WiredNativeSharedVariable Project(WiredSharedVariable shared)
    {
        var definition = shared.Definition;
        var endpoint = new WiredNativeEndpoint(new(definition, definition.HasValue, false), definition, null, true, false);
        var variable = WiredNativeCatalogProjection.Project(endpoint, null) with { Id = WiredVariableDefinitions.SharedId(shared.RoomId, definition.Target, definition.ItemId) };

        return new(shared.RoomId, shared.RoomName, variable);
    }
}

internal sealed record WiredNativeEndpoint(WiredVariableDescription Description, WiredVariableDefinition? Terminal,
    WiredVariableReference? Builtin, bool Writable, bool Converted);

internal static class WiredNativeCatalogProjection
{
    private const string BuiltinCatalogueResource = "Plus.Wired.BuiltinCatalogue.json";

    // Declared before Builtins: static fields initialise in textual order.
    private static readonly Lazy<IReadOnlyDictionary<string, BuiltinListing>> BuiltinListings = new(LoadBuiltinListings);

    /// <summary>
    /// The built-in (@) and smart (~) variables every room catalogue lists, under Habbo's fixed ids. The AIR variable pickers
    /// (NewVariablePicker.filteredAllVariables, WIN63-202610131318) list only the room catalogue, so these rows are what puts
    /// "@index" or "@direction" in the Echo, Give, Change and condition pickers. Type 1 is class_4027.INTERNAL.
    /// </summary>
    internal static ImmutableArray<WiredNativeVariable> Builtins { get; } = [.. WiredHabboVariableIds.All.Select(Builtin)];

    /// <summary>
    /// Habbo's listing of each built-in row (WiredBuiltinCatalogue.json, decoded from hhus WiredAllVariablesDiffs on AIR
    /// WIN63-202610131318-860371703): its type (3 for every ~ row), whether it is always available or invisible, and its text
    /// connector. An empty connector is still a connector: the AIR client then renders @position as "x: 8, y: 8".
    /// </summary>
    private sealed record BuiltinListing(int Type, bool AlwaysAvailable, bool IsInvisible,
        ImmutableArray<KeyValuePair<int, string>>? Connector, string? TextConnectorFallback);

    private static WiredNativeVariable Builtin((string Id, WiredVariableTarget Target, string Token) entry)
    {
        var reference = new WiredVariableReference(entry.Target, entry.Token);
        var hasValue = RoomWiredBuiltinVariables.HasNumericValue(reference);
        var name = entry.Token["internal:".Length..];
        var target = CatalogTarget(entry.Target);

        if (!BuiltinListings.Value.TryGetValue(entry.Id, out var listing)) {
            throw new InvalidOperationException($"Built-in variable {entry.Id} ({name}) has no row in {BuiltinCatalogueResource}.");
        }

        // The behaviour flags stay ours: they gate what this server can actually read, write or create.
        return new(entry.Id, listing.Type, name, 999, target, listing.AlwaysAvailable,
            RoomWiredBuiltinVariables.SupportsPresenceMutation(reference), hasValue,
            hasValue && RoomWiredBuiltinVariables.SupportsNumericWrite(reference),
            Array.IndexOf(Modern.Actions.WiredProjectileFlights.Intercepted, RoomWiredBuiltinVariables.Normalize(reference.Token)) >= 0,
            listing.IsInvisible, false, false,
            listing.Connector)
        { TextConnectorFallback = listing.TextConnectorFallback };
    }

    private static IReadOnlyDictionary<string, BuiltinListing> LoadBuiltinListings()
    {
        using var stream = typeof(WiredNativeCatalogProjection).Assembly.GetManifestResourceStream(BuiltinCatalogueResource)
            ?? throw new InvalidOperationException($"Embedded resource {BuiltinCatalogueResource} is missing.");
        using var document = System.Text.Json.JsonDocument.Parse(stream);
        var listings = new Dictionary<string, BuiltinListing>(StringComparer.Ordinal);

        foreach (var row in document.RootElement.GetProperty("rows").EnumerateArray()) {
            ImmutableArray<KeyValuePair<int, string>>? connector = row.TryGetProperty("textConnector", out var pairs)
                ? [.. pairs.EnumerateArray().Select(pair => new KeyValuePair<int, string>(pair[0].GetInt32(), pair[1].GetString()!))]
                : null;
            var fallback = row.TryGetProperty("textConnectorFallback", out var text) ? text.GetString() : null;

            listings.Add(row.GetProperty("id").GetString()!, new(row.GetProperty("type").GetInt32(),
                row.GetProperty("alwaysAvailable").GetBoolean(), row.GetProperty("isInvisible").GetBoolean(), connector, fallback));
        }

        return listings;
    }

    private static int CatalogTarget(WiredVariableTarget target) => target switch
    {
        WiredVariableTarget.Furni => 0,
        WiredVariableTarget.User => 1,
        WiredVariableTarget.Global => -10,
        _ => -20
    };

    private const int ReferenceAvailability = 20;

    internal static WiredNativeVariable Project(WiredNativeEndpoint endpoint, ImmutableArray<KeyValuePair<int, string>>? connector)
    {
        var description = endpoint.Description;
        var definition = description.Definition;
        var derived = description.IsDerived || endpoint.Converted;
        var builtin = endpoint.Builtin;
        var hasValue = description.HasValue;
        // A sub-variable is writable only when its derivation writes through to the parent (Time Utilities Value mode).
        var writable = (!derived || description.IsDerived) && endpoint.Writable;
        var presence = writable && !description.IsDerived && (builtin is { } reference ? RoomWiredBuiltinVariables.SupportsPresenceMutation(reference)
            : endpoint.Terminal?.Target is WiredVariableTarget.User or WiredVariableTarget.Furni or WiredVariableTarget.Context);
        var timestamps = !derived && builtin is null && endpoint.Terminal is not null;
        var target = CatalogTarget(definition.Target);

        // Observed on hhus: the lvlup sub-variable "<parent>.current_level" is listed with type 2 and availability 999.
        // Observed on hhus (AIR WIN63-202610131318-860371703): a global lists canReadCreationTime false, canReadLastUpdateTime true;
        // a Time Utilities Value part is writable and interceptable.
        // Observed on AIR WIN63-202610201032-107964540: a reference to a shared variable of another room is listed as a normal variable
        // (type 0) of availability 20 whose changes can be intercepted. An echo or alias of a variable of this room stays type 2.
        var crossRoom = definition.Link is { } link && link.SourceRoomId != definition.RoomId;
        var variable = new WiredNativeVariable(description.CatalogId, description.IsDerived || definition.Link is not null && !crossRoom ? 2 : 0,
            definition.Name, builtin is not null || description.IsDerived || definition.Target == WiredVariableTarget.Context ? 999
                : crossRoom ? ReferenceAvailability : (int)(endpoint.Terminal?.Availability ?? definition.Availability), target,
            !derived && builtin is null && endpoint.Terminal is { Target: WiredVariableTarget.Global, HasValue: true },
            presence, hasValue, writable && hasValue && (builtin is null || RoomWiredBuiltinVariables.SupportsNumericWrite(builtin)),
            (definition.Link is null || crossRoom) && (writable || description.Interceptable) && builtin is null, false,
            timestamps && definition.Target != WiredVariableTarget.Global, timestamps, connector);

        // Observed on AIR WIN63-202610201032-107964540: a pointer is listed as a normal variable of availability 999 that can be created,
        // deleted and written but not intercepted, with an empty text connector, and it requires an execution context.
        return definition.IsPointer
            ? variable with { Availability = 999, CanCreateAndDelete = true, CanWriteValue = true, CanInterceptChanges = false, Connector = [], RequiresExecutionContext = true }
            : variable;
    }
}
