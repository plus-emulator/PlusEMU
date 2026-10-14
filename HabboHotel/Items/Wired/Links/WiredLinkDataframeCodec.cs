using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>
/// The link dataframe text, as LinkDataframeCodec writes it (WIN63-202610131318-860371703,
/// wired_setup/uibuilder/presets/applications/LinkDataframeCodec.as:31-130): a JSON array of [key, text] pairs, one per used row.
/// </summary>
public static partial class WiredLinkDataframeCodec
{
    public const int MaxRows = 12;
    public const int MaxTextBytes = 3000;
    public const int MaxMessageBytes = 200;
    public const int MaxLinkMessageBytes = 1000;
    public const int MaxJoinedInts = 400;
    public const int MaxJoinedIds = 240;

    // Send row modes (LinkDataframeCodec NUMBER_LITERAL .. CUSTOM_MESSAGE) and receive row kinds (placeholder, variable).
    public const int NumberLiteral = 0;
    public const int NumberVariable = 1;
    public const int VariableText = 2;
    public const int CustomMessage = 3;
    public const int PlaceholderDestination = 0;
    public const int VariableDestination = 1;

    [GeneratedRegex("^\\w+$", RegexOptions.CultureInvariant)]
    private static partial Regex Word();

    public static int Utf8Length(string text) => Encoding.UTF8.GetByteCount(text);

    /// <summary>normalizeKey: underscores read as spaces, the ends are trimmed, inner spaces become underscores, lower case.</summary>
    public static string NormalizeKey(string key) => Regex.Replace(key.Replace('_', ' '), "^[\\x00- ]+|[\\x00- ]+$", "")
        .Replace(' ', '_').ToLowerInvariant();

    public static bool IsKey(string key)
    {
        var normalized = NormalizeKey(key);

        return normalized.Length is >= 1 and <= 40 && Word().IsMatch(normalized);
    }

    /// <summary>placeholderError: 1 to 40 word characters and not the trigger's own placeholder.</summary>
    public static bool IsPlaceholder(string name) => name.Length is >= 1 and <= 40 && Word().IsMatch(name)
        && !string.Equals(name, "trigger_text", StringComparison.OrdinalIgnoreCase);

    public static bool IsTarget(int target) => target is 0 or 1 or -10 or -20;

    /// <summary>decode(stringParam, count): an empty text is zero rows; otherwise exactly count [string, string] pairs.</summary>
    public static ImmutableArray<(string Key, string Text)>? Decode(string text, int count)
    {
        if (count is < 0 or > MaxRows) {
            return null;
        }

        if (text.Length == 0 && count == 0) {
            return [];
        }

        try {
            using var document = JsonDocument.Parse(text);

            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != count) {
                return null;
            }

            var rows = ImmutableArray.CreateBuilder<(string, string)>(count);

            foreach (var row in document.RootElement.EnumerateArray()) {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != 2
                    || row[0].ValueKind != JsonValueKind.String || row[1].ValueKind != JsonValueKind.String) {
                    return null;
                }

                rows.Add((row[0].GetString()!, row[1].GetString()!));
            }

            return rows.MoveToImmutable();
        }
        catch (JsonException) {
            return null;
        }
    }

    public static string Encode(IEnumerable<(string Key, string Text)> rows) =>
        JsonSerializer.Serialize(rows.Select(row => new[] { row.Key, row.Text }));

    /// <summary>storageError: the text, and the int and id lists joined with '|', fit the client's storage limits.</summary>
    public static bool FitsStorage(string text, IEnumerable<int> ints, IEnumerable<string> ids) =>
        Utf8Length(text) <= MaxTextBytes && string.Join('|', ints).Length <= MaxJoinedInts && string.Join('|', ids).Length <= MaxJoinedIds;
}
