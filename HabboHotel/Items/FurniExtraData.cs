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

    // A room background shows the image a branding staff member typed and opens its click address (AIR RoomBrandingLogic).
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

        // Observed on hhus (AIR WIN63-202610131318-860371703): every firing flips a box's legacy state between "1" and "0" (says something,
        // periodically, long periodically and walks on alike), and a box that never fired rests at "0". A box keeps legacy data in the
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
                return keepLegacy ? new LegacyDataFormat { Data = stored.Length == 0 && StartsAtZero(definition) ? "0" : stored } : FurniObjectData.Empty;
        }
    }

    // Observed on AIR WIN63-202610151745-318700621: a new Wired counter, floor switch, antenna or colour tile has state "0", never "".
    // Game furni such as freeze blocks, scoreboards and game timers use an empty state for their own meaning, so they keep it.
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
