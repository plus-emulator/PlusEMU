using System.Globalization;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>The keys a Send Message, Send Dataframe or Receive Dataframe save is refused with.</summary>
internal static class WiredLinkSaveRules
{
    private const string InvalidDataframeKey = "wiredfurni.error.link.invalid_dataframe";
    private const string KeyLengthKey = "wiredfurni.error.link.key_length";
    private const string KeySyntaxKey = "wiredfurni.error.variables.name_syntax";
    private const string KeyUniqueKey = "wiredfurni.error.variables.name_uniq";
    private const string DestinationConflictKey = "wiredfurni.error.link.destination_conflict";
    private const string CaptureVariableKey = "wiredfurni.error.link.invalid_capture_variable";
    private const string PlaceholderReservedKey = "wiredfurni.error.placeholder.reserved";
    private const string TooMuchTextKey = "wiredfurni.error.too_much_text";

    public static (string Key, IReadOnlyList<KeyValuePair<string, string>> Parameters)? Refusal(string name, WiredNativeEditorConfiguration native)
    {
        var p = native.OwnedIntParams;

        return name switch
        {
            "wf_act_send_message" when p.Length == 1 => SendMessage(p[0], native.Text),
            "wf_act_send_dataframe" when p.Length == 37 => SendDataframe(native),
            "wf_trg_receive_dataframe" when p.Length == 13 => ReceiveDataframe(native),
            _ => null
        };
    }

    private static (string, IReadOnlyList<KeyValuePair<string, string>>)? Plain(string? key) => key == null ? null : (key, []);

    private static (string, IReadOnlyList<KeyValuePair<string, string>>)? TooMuch(int limit) =>
        (TooMuchTextKey, [new("limit", limit.ToString(CultureInfo.InvariantCulture))]);

    private static (string, IReadOnlyList<KeyValuePair<string, string>>)? SendMessage(int source, string text)
    {
        if (source is not (0 or 1)) {
            return Plain(InvalidDataframeKey);
        }

        return WiredLinkDataframeCodec.Utf8Length(text) > WiredLinkDataframeCodec.MaxLinkMessageBytes
            ? TooMuch(WiredLinkDataframeCodec.MaxLinkMessageBytes) : null;
    }

    private static (string, IReadOnlyList<KeyValuePair<string, string>>)? SendDataframe(WiredNativeEditorConfiguration native)
    {
        var p = native.OwnedIntParams;

        if (WiredLinkDataframeCodec.Decode(native.Text, p[0]) is not { } rows) {
            return Plain(InvalidDataframeKey);
        }

        for (var row = 0; row < 12; row++) {
            if (p[1 + row * 3] is < 0 or > 3 || !WiredLinkDataframeCodec.IsTarget(p[3 + row * 3])) {
                return Plain(InvalidDataframeKey);
            }
        }

        if (KeyRefusal(rows.Select(row => row.Key)) is { } keyRefusal) {
            return Plain(keyRefusal);
        }

        for (var row = 0; row < rows.Length; row++) {
            if (p[1 + row * 3] == WiredLinkDataframeCodec.CustomMessage
                && WiredLinkDataframeCodec.Utf8Length(rows[row].Text) > WiredLinkDataframeCodec.MaxMessageBytes) {
                return TooMuch(WiredLinkDataframeCodec.MaxMessageBytes);
            }
        }

        return null;
    }

    private static (string, IReadOnlyList<KeyValuePair<string, string>>)? ReceiveDataframe(WiredNativeEditorConfiguration native)
    {
        var p = native.OwnedIntParams;

        if (WiredLinkDataframeCodec.Decode(native.Text, p[0]) is not { } rows || p.Skip(1).Any(kind => kind is not (0 or 1))) {
            return Plain(InvalidDataframeKey);
        }

        if (KeyRefusal(rows.Select(row => row.Key)) is { } keyRefusal) {
            return Plain(keyRefusal);
        }

        var placeholders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var row = 0; row < rows.Length; row++) {
            if (p[1 + row] == WiredLinkDataframeCodec.VariableDestination) {
                if (row >= native.VariableIds.Length || WiredVariableAbsent.Is(native.VariableIds[row])) {
                    return Plain(CaptureVariableKey);
                }

                continue;
            }

            var placeholder = rows[row].Text;

            if (string.Equals(placeholder, "trigger_text", StringComparison.OrdinalIgnoreCase)) {
                return Plain(PlaceholderReservedKey);
            }

            if (placeholder.Length is 0 or > WiredTextAddonRules.MaxNameLength) {
                return Plain(WiredTextAddonRules.NameLengthKey);
            }

            if (!placeholders.Add(placeholder)) {
                return Plain(DestinationConflictKey);
            }
        }

        return null;
    }

    private static string? KeyRefusal(IEnumerable<string> keys)
    {
        var seen = new HashSet<string>();
        var duplicate = false;

        foreach (var key in keys) {
            var normalized = WiredLinkDataframeCodec.NormalizeKey(key);

            if (normalized.Length is 0 or > WiredTextAddonRules.MaxNameLength) {
                return KeyLengthKey;
            }

            if (!WiredLinkDataframeCodec.IsKey(key)) {
                return KeySyntaxKey;
            }

            duplicate |= !seen.Add(normalized);
        }

        return duplicate ? KeyUniqueKey : null;
    }
}
