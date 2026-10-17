using System.Text.RegularExpressions;

namespace Plus.HabboHotel.Items.FootballGate;

/// <summary>
/// The football gate's stuff data is one legacy string, "&lt;male look&gt;,&lt;female look&gt;". The client splits it on "," and
/// takes index 0 as furniture_clothing_boy and index 1 as furniture_clothing_girl (AIR WIN63-202610151745-318700621,
/// com/sulake/habbo/room/object/logic/furniture/FurnitureClothingChangeLogic.as:51-66).
/// </summary>
public static partial class FootballGateLooks
{
    public const string Male = "M";
    public const string Female = "F";
    private const int MaxParts = 32;

    // The editor seeds the gate with a placeholder head (hd-99999-99999) and only edits torso and legs
    // (FurnitureClothingChangeWidgetHandler.as:100-115), so the gate's head is never put on an avatar.
    private const string HeadSetType = "hd";

    public static (string Male, string Female) Parse(string? data)
    {
        if (string.IsNullOrEmpty(data)) {
            return ("", "");
        }

        var fields = data.Split(',');

        return (Sanitize(fields[0]), fields.Length > 1 ? Sanitize(fields[1]) : "");
    }

    public static string Serialize(string male, string female) => $"{male},{female}";

    public static string LookFor(string? data, string gender)
    {
        var (male, female) = Parse(data);

        return string.Equals(gender, Female, StringComparison.OrdinalIgnoreCase) ? female : male;
    }

    /// <summary>Keeps well-formed figure parts ("ch-3109-92-1408") once per set type, in their order. Anything else is dropped.</summary>
    public static string Sanitize(string? look)
    {
        if (string.IsNullOrWhiteSpace(look)) {
            return "";
        }

        var parts = new List<string>();
        var types = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in look.Trim().ToLowerInvariant().Split('.', StringSplitOptions.RemoveEmptyEntries)) {
            if (parts.Count == MaxParts || !FigurePart().IsMatch(part) || !types.Add(part[..2])) {
                continue;
            }

            parts.Add(part);
        }

        return string.Join('.', parts);
    }

    /// <summary>
    /// The gate's parts replace the user's parts of the same set type; every other part of the user's look is kept.
    /// The gate's placeholder head is never applied.
    /// </summary>
    public static string Merge(string userLook, string gateLook)
    {
        var gateParts = Sanitize(gateLook).Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => part[..2] != HeadSetType).ToList();
        var replacements = gateParts.ToDictionary(part => part[..2], StringComparer.Ordinal);
        var applied = new HashSet<string>(StringComparer.Ordinal);
        var merged = new List<string>();

        foreach (var part in userLook.Split('.', StringSplitOptions.RemoveEmptyEntries)) {
            var type = part.Split('-')[0];

            if (replacements.TryGetValue(type, out var replacement)) {
                if (applied.Add(type)) {
                    merged.Add(replacement);
                }
            }
            else {
                merged.Add(part);
            }
        }

        merged.AddRange(gateParts.Where(part => !applied.Contains(part[..2])));

        return string.Join('.', merged);
    }

    [GeneratedRegex("^[a-z]{2}-[0-9]{1,9}(-[0-9]{1,9}){0,2}$")]
    private static partial Regex FigurePart();
}
