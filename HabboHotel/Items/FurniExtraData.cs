using System.Text.RegularExpressions;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items;

internal static class FurniExtraData
{
    private static readonly HashSet<string> ClientImageKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "w", "url", "id", "imageUrl", "href", "src", "clickUrl"
    };

    private static readonly Regex ExternalAddress = new(
        @"(?:https?:|data:|blob:|javascript:|file:|//)|\u0000",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmbeddedImageKey = new(
        "\"(?:w|url|id|imageUrl|href|src|clickUrl)\"\\s*:",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly HashSet<string> BackgroundAddressKeys = new(StringComparer.Ordinal) { "imageUrl", "clickUrl" };

    // A room background shows the image a branding staff member typed and opens its click address.
    // Only plain http(s) addresses pass; every other key keeps the client image rules.
    public static bool RejectsBackground(IReadOnlyList<string> values)
    {
        var rest = new List<string>();

        for (var index = 0; index + 1 < values.Count; index += 2) {
            if (!BackgroundAddressKeys.Contains(values[index])) {
                rest.Add(values[index]);
                rest.Add(values[index + 1]);
            }
            else if (!IsWebAddress(values[index + 1] ?? "")) {
                return true;
            }
        }

        return values.Count % 2 != 0 || RejectsClientImage(rest);
    }

    private static bool IsWebAddress(string value) => value.Length == 0
        || (!value.Any(character => char.IsWhiteSpace(character) || char.IsControl(character) || character == '\\')
            && Uri.TryCreate(value, UriKind.Absolute, out var address)
            && (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps));

    public const string BackgroundAllowedHostsSetting = "room.background.allowed_hosts";

    private const int MaxBackgroundAddressLength = 500;

    // A room background keeps imageUrl, offsetX, offsetY and offsetZ; scale and alpha are the Volt image position editor's own settings.
    private static readonly Dictionary<string, (int Min, int Max)> BackgroundNumbers = new(StringComparer.Ordinal)
    {
        ["offsetX"] = (-10000, 10000),
        ["offsetY"] = (-10000, 10000),
        ["offsetZ"] = (-1000, 1000),
        ["scale"] = (10, 500),
        ["alpha"] = (0, 100)
    };

    /// <summary>
    /// Turns the client's flat key,value list into the data a background keeps: the known keys only, numbers clamped to their range
    /// and the image address checked against the host list. Returns null when a value cannot be kept. Run after <see cref="RejectsBackground"/>.
    /// </summary>
    public static Dictionary<string, string>? NormalizeBackground(IReadOnlyList<string> values, IReadOnlyCollection<string> allowedHosts)
    {
        var pairs = new Dictionary<string, string> { ["state"] = "0" };

        for (var index = 0; index + 1 < values.Count; index += 2) {
            var key = values[index];
            var value = (values[index + 1] ?? "").Trim();

            if (key is "imageUrl" or "clickUrl") {
                if (value.Length > MaxBackgroundAddressLength || key == "imageUrl" && !IsAllowedHost(value, allowedHosts)) {
                    return null;
                }

                pairs[key] = value;
            }
            else if (BackgroundNumbers.TryGetValue(key, out var range)) {
                if (value.Length == 0) {
                    if (key.StartsWith("offset", StringComparison.Ordinal)) {
                        pairs[key] = "0";
                    }

                    continue;
                }

                if (!long.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out var number)) {
                    return null;
                }

                pairs[key] = Math.Clamp(number, range.Min, range.Max).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return pairs;
    }

    /// <summary>Splits the host list setting: comma separated, a leading "*." is optional and case is ignored. An absent setting reads "0" and means no list.</summary>
    public static IReadOnlyCollection<string> ParseAllowedHosts(string? setting) =>
        (setting ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => entry != "0")
            .Select(entry => entry.TrimStart('*').Trim('.').ToLowerInvariant())
            .Where(entry => entry.Length > 0)
            .Distinct()
            .ToArray();

    private static bool IsAllowedHost(string address, IReadOnlyCollection<string> allowedHosts)
    {
        if (address.Length == 0 || allowedHosts.Count == 0) {
            return true;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0) {
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();

        return allowedHosts.Any(allowed => host == allowed || host.EndsWith("." + allowed, StringComparison.Ordinal));
    }

    public static bool RejectsClientImage(IReadOnlyList<string> values)
    {
        for (var index = 0; index < values.Count; index++) {
            var value = values[index] ?? "";

            if (index % 2 == 0 && ClientImageKeys.Contains(value)) {
                return true;
            }

            if (ExternalAddress.IsMatch(value) || EmbeddedImageKey.IsMatch(value)) {
                return true;
            }
        }

        // Branding is decoded by MapDataFormat.Store. Validate exactly that representation
        // as well, including escaped keys, values and injected pair separators.
        var decoded = new MapDataFormat();

        try {
            decoded.Store(Branding(values));
        }
        catch (ArgumentException) {
            return true;
        }

        return decoded.Data.Any(pair => ClientImageKeys.Contains(pair.Key)
            || ExternalAddress.IsMatch(pair.Key) || ExternalAddress.IsMatch(pair.Value)
            || EmbeddedImageKey.IsMatch(pair.Key) || EmbeddedImageKey.IsMatch(pair.Value));
    }

    public static IFurniObjectData Load(ItemDefinition definition, string stored, bool keepLegacy)
    {
        stored ??= "";

        if (definition.Type == ItemType.Floor && HighscoreDataFormat.TryDefinition(definition.ItemName, out var scoreType, out var clearType)) {
            var board = new HighscoreDataFormat { ScoreType = scoreType, ClearType = clearType };

            // Legacy newly-created boards carry only their switch state; typed captured payloads never fall back to empty.
            if (stored is "" or "0" or "1") {
                board.State = stored;
            }
            else {
                board.Store(stored);

                if (board.ScoreType != scoreType || board.ClearType != clearType) {
                    throw new ArgumentException("Highscore types do not match the furniture definition.", nameof(stored));
                }
            }

            return board;
        }

        // Every firing flips a box's legacy state between "1" and "0", and a box that never fired rests at "0". A box keeps legacy data in the
        // inventory too, so a placed box can always flash.
        if (IsWiredBox(definition)) {
            return new LegacyDataFormat { Data = stored.Length == 0 ? "0" : stored };
        }

        // A recolorable keeps its colour in the inventory too, so a picked up block is placed again in the same colour.
        if (definition.InteractionType == InteractionType.Recolorable) {
            var recolorable = new RecolorableDataFormat();
            recolorable.Store(stored);

            return recolorable;
        }

        switch (definition.InteractionType) {
            case InteractionType.CrackableEgg: {
                    var data = definition.CreateData();

                    if (stored.Length > 0) {
                        data.Store(stored);
                    }

                    return data;
                }
            case InteractionType.AreaHide:
                return AreaHide.AreaHideState.Load(stored);
            case InteractionType.Background: {
                    var data = definition.CreateData();

                    if (stored.Length > 0) {
                        data.Store(BackgroundPairs(stored));
                    }

                    return data;
                }
            default:
                return keepLegacy ? new LegacyDataFormat { Data = LegacyState(definition, definition.ItemName == "wf_pyramid" ? "" : stored) } : FurniObjectData.Empty;
        }
    }

    // A new Wired counter, floor switch, antenna or colour tile has state "0", never "".
    // Game furni such as freeze blocks, scoreboards and game timers use an empty state for their own meaning, so they keep it.
    // A new wf_blob or wf_pyramid has state 1, a new game tile, box or button has no state, and a new glass door, fire gate or 8 direction arrow plate has state 0.
    internal static string LegacyState(ItemDefinition definition, string stored) => stored.Length > 0 ? stored : StartState(definition) ?? stored;

    private static string? StartState(ItemDefinition definition)
    {
        if (definition.Type != ItemType.Floor) {
            return null;
        }

        var name = definition.ItemName;

        if (name is "wf_blob" or "wf_pyramid") {
            return "1";
        }

        if (name is "wf_tile1" or "wf_tile2" or "wf_box" or "wf_button" or "wf_button_red" or "wf_button_x") {
            return "";
        }

        if (name is "wf_glassdoor" or "wf_firegate" or "wf_arrowplate_8dir") {
            return "0";
        }

        return StartsAtZero(definition) ? "0" : null;
    }

    private static bool StartsAtZero(ItemDefinition definition) => definition.Type == ItemType.Floor
        && (Wired.Modern.Actions.WiredCounterController.IsWiredCounter(definition)
            || definition.Modes > 1 && definition.InteractionType is InteractionType.None or InteractionType.WfFloorSwitch1 or InteractionType.WfFloorSwitch2);

    private static bool IsWiredBox(ItemDefinition definition) => definition.InteractionType is InteractionType.WiredTrigger
        or InteractionType.WiredCondition or InteractionType.WiredEffect or InteractionType.WiredSelector or InteractionType.WiredAddon
        or InteractionType.WiredVariable;

    public static string BackgroundPairs(string stored)
    {
        if (stored.Contains('\n')) {
            return stored;
        }

        var fields = stored.Split('\t');

        if (fields.Length < 2 || fields.Length % 2 != 0) {
            return stored;
        }

        return string.Join("\n", Enumerable.Range(0, fields.Length / 2).Select(index => $"{fields[index * 2]}\t{fields[index * 2 + 1]}"));
    }

    public static string Branding(IReadOnlyList<string> values)
    {
        var fields = new List<string> { "state", "0" };
        fields.AddRange(values);
        var pairs = fields.Count / 2;

        return string.Join("\n", Enumerable.Range(0, pairs).Select(index => $"{fields[index * 2]}\t{fields[index * 2 + 1]}"));
    }
}
