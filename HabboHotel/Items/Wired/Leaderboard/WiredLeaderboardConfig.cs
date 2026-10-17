using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace Plus.HabboHotel.Items.Wired.Leaderboard;

/// <summary>One leaderboard column: its type (0 username, 1 value, 2 last update, 3 creation, 4 level, 5 text connection), label and width.</summary>
public sealed record WiredLeaderboardColumn(int Type, string Label, int Width);

/// <summary>A display-name override for one of the stack's variables.</summary>
public sealed record WiredLeaderboardOverride(string VariableId, string Name);

/// <summary>
/// The Variable Leaderboard add-on's settings, as VariableLeaderboardConfigCodec (WIN63-202610131318-860371703,
/// wired_setup/addons/leaderboard/VariableLeaderboardConfigCodec.as:182-222 encode, :65 decode, :246-392 validation) writes them:
/// 19 ints, 12 variable ids and a TAB-joined text of 17 fields. An empty text is the unpublished board.
/// Kept apart from the overlay runtime so live hhus observations can adjust the rules in one place.
/// </summary>
public sealed record WiredLeaderboardConfig
{
    public const int IntCount = 19;
    public const int VariableCount = 12;
    public const int TextFields = 17;
    public const int MaxColumns = 4;
    public const int MaxOverrides = 10;
    public const int MaxTextBytes = 1000;
    public const int MaxJoinedInts = 400;
    public const int MaxJoinedIds = 240;
    public const string NoVariable = "0";
    public static readonly ImmutableArray<int> PageSizes = [10, 15, 20];

    // Column defaults of an unpublished board: Username XL and Value Small (Codec.decodeUnpublished, :350).
    public static readonly ImmutableArray<int> DefaultInts = [0, 2, 0, 1, -1, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 0, 0, 0];

    public int Sort { get; init; }
    public ImmutableArray<WiredLeaderboardColumn> Columns { get; init; } = [];
    public bool Filter { get; init; }
    public bool Comparison { get; init; }
    public int OperandKind { get; init; }
    public int TileX { get; init; }
    public int TileY { get; init; }
    public int OffsetX { get; init; }
    public int Altitude { get; init; }
    public int PageSizeOption { get; init; }
    public string FilterVariableId { get; init; } = NoVariable;
    public string GlobalOperandVariableId { get; init; } = NoVariable;
    public long? Literal { get; init; }
    public ImmutableArray<WiredLeaderboardOverride> Overrides { get; init; } = [];
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Published => Title.Length > 0;
    public int PageSize => PageSizes[PageSizeOption];

    public static int DefaultWidth(int type) => type switch { 0 => 3, 2 or 3 => 1, 5 => 2, _ => 0 };

    /// <summary>
    /// The board's text as the hotel keeps it: 17 TAB fields, with the fields of unused slots emptied. Observed on AIR
    /// WIN63-202610151745-318700621: "lb probe\t\tValue\t\t\t\t\tOwner" (one column, no override) is accepted and reads back as
    /// "lb probe\t\tValue" and 14 empty fields; the override name in an unused slot is dropped. Fields past the 17th are dropped.
    /// </summary>
    public static string Normalize(string text, ImmutableArray<int> p)
    {
        if (text.Length == 0 || p.Length != IntCount) {
            return text;
        }

        var fields = text.Split('\t').Concat(Enumerable.Repeat("", TextFields)).Take(TextFields).ToArray();

        for (var slot = p[1]; slot < MaxColumns; slot++) {
            fields[2 + slot] = "";
        }

        if (!(p[7] == 1 && p[8] == 0)) {
            fields[6] = "";
        }

        for (var index = Math.Max(0, p[9]); index < MaxOverrides; index++) {
            fields[7 + index] = "";
        }

        return string.Join('\t', fields);
    }

    /// <summary>The add-on's save, checked the way the client codec validates it; null when the client would refuse it.</summary>
    public static WiredLeaderboardConfig? Decode(ImmutableArray<int> p, ImmutableArray<string> ids, string text)
    {
        if (p.Length != IntCount || ids.Length != VariableCount || string.Join('|', p).Length > MaxJoinedInts
            || string.Join('|', ids).Length > MaxJoinedIds || Encoding.UTF8.GetByteCount(text) > MaxTextBytes) {
            return null;
        }

        if (p[0] is < 0 or > 5 || p[1] is < 1 or > MaxColumns || p[6] is not (0 or 1) || p[7] is not (0 or 1) || p[8] is not (0 or 1)
            || p[9] is < 0 or > MaxOverrides || p[12] is < -32 or > 32 || p[18] is < 0 or > 2) {
            return null;
        }

        var filter = p[6] == 1;
        var comparison = p[7] == 1;

        // Conditional fields are canonical (Codec.validateConditionalFields, :355-392): off sections carry zeroes and "0".
        if (!filter && (comparison || p[8] != 0) || !comparison && p[8] != 0) {
            return null;
        }

        var count = p[1];
        var types = new List<int>();

        for (var slot = 0; slot < MaxColumns; slot++) {
            var type = p[2 + slot];
            var width = p[14 + slot];

            if (slot < count ? type is < 0 or > 5 || width is < 0 or > 3 || types.Contains(type) || (slot == 0) != (type == 0)
                : type != -1 || width != 0) {
                return null;
            }

            if (slot < count) {
                types.Add(type);
            }
        }

        string Id(int index) => ids[index] is "" ? NoVariable : ids[index];
        bool Absent(int index) => Id(index) == NoVariable;

        if (filter == Absent(0) || (comparison && p[8] == 1) == Absent(1)) {
            return null;
        }

        for (var index = 0; index < MaxOverrides; index++) {
            if (index < p[9] == Absent(2 + index)) {
                return null;
            }
        }

        var overrideIds = Enumerable.Range(0, p[9]).Select(index => Id(2 + index)).ToArray();

        var config = new WiredLeaderboardConfig
        {
            Sort = p[0],
            Filter = filter,
            Comparison = comparison,
            OperandKind = p[8],
            TileX = p[10],
            TileY = p[11],
            OffsetX = p[12],
            Altitude = p[13],
            PageSizeOption = p[18],
            FilterVariableId = Id(0),
            GlobalOperandVariableId = Id(1)
        };

        // The unpublished board has no text: defaults stand in for the strings.
        if (text.Length == 0) {
            return config with
            {
                Columns = [.. types.Select((type, slot) => new WiredLeaderboardColumn(type, "", p[14 + slot]))],
                Overrides = [.. overrideIds.Select(id => new WiredLeaderboardOverride(id, ""))]
            };
        }

        var fields = Normalize(text, p).Split('\t');

        if (fields[0].Length is < 1 or > 50 || fields[1].Length > 80) {
            return null;
        }

        var columns = new List<WiredLeaderboardColumn>();

        for (var slot = 0; slot < MaxColumns; slot++) {
            var label = fields[2 + slot];

            if (slot < count ? label.Length is < 2 or > 12 : label.Length != 0) {
                return null;
            }

            if (slot < count) {
                columns.Add(new(types[slot], label, p[14 + slot]));
            }
        }

        long? literal = null;

        if (comparison && p[8] == 0) {
            if (!long.TryParse(fields[6], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                || value.ToString(CultureInfo.InvariantCulture) != fields[6]) {
                return null;
            }

            literal = value;
        }
        else if (fields[6].Length != 0) {
            return null;
        }

        var overrides = new List<WiredLeaderboardOverride>();

        for (var index = 0; index < MaxOverrides; index++) {
            var name = fields[7 + index];

            if (index < p[9] ? name.Length is < 1 or > 22 : name.Length != 0) {
                return null;
            }

            if (index < p[9]) {
                overrides.Add(new(overrideIds[index], name));
            }
        }

        return config with
        {
            Columns = [.. columns],
            Literal = literal,
            Overrides = [.. overrides],
            Title = fields[0],
            Description = fields[1]
        };
    }

    /// <summary>VariableLeaderboardDisplayNameFormatter.suggest: separators become spaces, each word capitalised; empty is "Variable".</summary>
    public static string SuggestName(string technicalName)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(technicalName, "[_-]+", " ")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant());
        var name = string.Join(' ', words);

        return name.Length == 0 ? "Variable" : name;
    }
}
