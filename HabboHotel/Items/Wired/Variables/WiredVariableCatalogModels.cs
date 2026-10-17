namespace Plus.HabboHotel.Items.Wired.Variables;

public sealed record WiredVariableDescription(WiredVariableDefinition Definition, bool HasValue, bool ReadOnly)
{
    public IReadOnlyDictionary<int, string> TextConnector { get; init; } = new Dictionary<int, string>();
    // The AIR client only reads this flag to keep a variable out of pickers used outside a running stack (Echo,
    // Variable Changed, FX override/audience, overview holder actions, leaderboard, pointer). Habbo sends it false
    // for every variable, context ones included, so context variables stay pickable there.
    public bool RequiresExecutionContext => false;
    public string? TextConnectorFallback { get; init; }
    public bool HasTextConnectorFallback => TextConnector.Count > 0 && TextConnectorFallback is not null;
    public bool IsBuiltin { get; init; }
    public bool IsDerived { get; init; }
    public bool CanCreateAndDelete => !ReadOnly && !IsBuiltin && !IsDerived && Definition.Target is WiredVariableTarget.User or WiredVariableTarget.Furni;
    public bool CanWriteValue => !ReadOnly && HasValue;
    public bool CanReadTimestamps => !IsBuiltin && !IsDerived;
    /// <summary>Observed on hhus (AIR WIN63-202610131318-860371703): a global variable lists canReadCreationTime false and canReadLastUpdateTime true.</summary>
    public bool CanReadCreationTime => CanReadTimestamps && Definition.Target != WiredVariableTarget.Global;
    public bool CanReadLastUpdateTime => CanReadTimestamps;
    // Catalog target/type codes differ from scalar editor target codes.
    public int CatalogTarget => Definition.Target switch
    {
        WiredVariableTarget.Global => 0,
        WiredVariableTarget.User => 1,
        WiredVariableTarget.Furni => 2,
        _ => 3
    };
    public string CatalogId => CatalogPrefix(Definition.Target) + Definition.ItemId;
    private static string CatalogPrefix(WiredVariableTarget target) => target switch
    {
        WiredVariableTarget.User => "user:",
        WiredVariableTarget.Furni => "furni:",
        WiredVariableTarget.Global => "room:",
        _ => "ctx:"
    };
    /// <summary>
    /// The one inverse of <see cref="CatalogId"/>: a definition id ("user:12" is custom:12) or a built-in's own token
    /// ("ctx:internal:@event.transaction_complete.multiplier"). The module still authorizes what the token names.
    /// </summary>
    public static bool TryParseCatalogId(string id, out WiredVariableTarget target, out string token)
    {
        token = "";
        target = default;

        // Habbo's own ids for built-in and smart variables ("-120", "~131").
        if (id is ['-' or '~', ..]) {
            return WiredHabboVariableIds.TryResolve(id, out target, out token);
        }

        foreach (var candidate in Enum.GetValues<WiredVariableTarget>()) {
            var prefix = CatalogPrefix(candidate);

            if (!id.StartsWith(prefix, StringComparison.Ordinal)) {
                continue;
            }

            var rest = id[prefix.Length..];

            if (uint.TryParse(rest, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var itemId)
                && itemId > 0 && itemId.ToString(System.Globalization.CultureInfo.InvariantCulture) == rest) {
                token = "custom:" + itemId;
            }
            else if (rest.Length > 10 && (rest.StartsWith("internal:@", StringComparison.Ordinal) || rest.StartsWith("internal:~", StringComparison.Ordinal))
                && rest.IndexOfAny(['\t', '\r', '\n']) < 0 && !WiredQuestVariables.IsToken(rest)) {
                token = rest;
            }
            else {
                continue;
            }

            target = candidate;

            return true;
        }

        return false;
    }
    public int Hash
    {
        get
        {
            var hash = StableHash(CatalogId);

            foreach (var value in new[] { StableHash(Definition.Name), CatalogTarget, (int)Definition.Availability, CatalogTarget,
                HasValue ? 1 : 0, TextConnector.Count > 0 ? 2 : 0, ReadOnly ? 4 : 0, IsBuiltin ? 1 : 0, IsDerived ? 1 : 0,
                RequiresExecutionContext ? 1 : 0 }) {
                hash = unchecked(hash * 31 + value);
            }

            foreach (var (key, value) in TextConnector.OrderBy(x => x.Key)) {
                hash = unchecked(hash * 31 + key);
                hash = unchecked(hash * 31 + StableHash(value));
            }

            if (TextConnector.Count > 0) {
                hash = unchecked(hash * 31 + (HasTextConnectorFallback ? 1 : 0));

                if (HasTextConnectorFallback) {
                    hash = unchecked(hash * 31 + StableHash(TextConnectorFallback!));
                }
            }

            return hash;
        }
    }
    private static int StableHash(string text)
    {
        var hash = 0;

        foreach (var character in text) {
            hash = unchecked(hash * 31 + character);
        }

        return hash;
    }
}

/// <summary>A request-scoped, authorized catalog. Hashes are deterministic across processes.</summary>
public sealed class WiredVariableCatalog(IReadOnlyList<WiredVariableDescription> variables)
{
    public IReadOnlyList<WiredVariableDescription> Variables { get; } = variables;
    public int Hash => Variables.Aggregate(Variables.Count, (hash, variable) => unchecked(hash * 31 + variable.Hash));
    public WiredVariableDescription? Find(string id) => Variables.FirstOrDefault(x => x.CatalogId == id);
    public IReadOnlyList<WiredVariableCatalogDiff> Diff(IReadOnlyDictionary<string, int> known)
    {
        var current = Variables.ToDictionary(x => x.CatalogId);
        var removed = known.Keys.Where(x => !current.ContainsKey(x)).Order(StringComparer.Ordinal).ToArray();
        var changed = Variables.Where(x => !known.TryGetValue(x.CatalogId, out var hash) || x.Hash != hash).ToArray();
        var count = Math.Max(1, (changed.Length + 99) / 100);

        return Enumerable.Range(0, count).Select(i => new WiredVariableCatalogDiff(Hash, i == count - 1,
            i == 0 ? removed : [], changed.Skip(i * 100).Take(100).ToArray())).ToArray();
    }
}
public sealed record WiredVariableCatalogDiff(int Hash, bool LastChunk, IReadOnlyList<string> Removed,
    IReadOnlyList<WiredVariableDescription> Changed);
public sealed record WiredVariableMenuSnapshot(uint RoomId, IReadOnlyList<WiredVariableDescription> Definitions,
    IReadOnlyList<WiredVariableStoredHolder> Assignments);
public sealed record WiredVariableStoredHolder(WiredVariableKey Key, string Name, WiredVariableValue Value);
public sealed record WiredVariableHolderPage(int Total, int Page, int PageSize, IReadOnlyList<WiredVariableStoredHolder> Holders);

internal static class WiredVariablePaging
{
    public static WiredVariableHolderPage Page(IEnumerable<WiredVariableStoredHolder> values, int page, int size, int sort)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var ordered = sort switch
        {
            0 => values.OrderBy(x => x.Value.Value).ThenBy(x => x.Key.HolderId),
            1 => values.OrderByDescending(x => x.Value.Value).ThenBy(x => x.Key.HolderId),
            2 => values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Key.HolderId),
            _ => values.OrderBy(x => x.Key.HolderId)
        };
        var all = ordered.ToArray();
        var offset = (int)Math.Min((long)(page - 1) * size, all.Length);

        return new(all.Length, page, size, all.Skip(offset).Take(size).ToArray());
    }
}

/// <summary>The AIR picker's "no variable" sentinel (WiredVariable "n"); an empty id means the same.</summary>
public static class WiredVariableAbsent
{
    public const string Id = "n";
    /// <summary>The wire id of a slot that names no variable. The client's own constant is "n"; the official hotel's editor frames carry "0".</summary>
    public const string WireId = "0";
    public static bool Is(string? id) => string.IsNullOrEmpty(id) || id == Id || id == WireId;
}
