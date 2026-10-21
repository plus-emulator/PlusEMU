using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// How the hotel stores a save of the add-ons, the two long timers, the User Performs Action boxes, Teleport to Room, Scan Chest, Match To Snapshot and the two chest conditions.
/// Observed on AIR WIN63-202610201032-107964540: a save with missing ints takes the frame default for each, extra ints and a text the box
/// does not read are dropped, and most out-of-range numbers are clamped or reset instead of refused.
/// </summary>
internal static class WiredEditorStoreRules
{
    public const string UserSourceKey = "wiredfurni.error.invalid_user_source";
    public const string ChestSourceKey = "wiredfurni.error.require_furni_chests";
    private const string ScanChest = "wf_xtra_scan_chest_furni_by_type";
    private const string TextVariable = "wf_xtra_text_output_variable";
    private const string TextFurniName = "wf_xtra_text_output_furni_name";
    private const string PlaceholderKey = "wiredfurni.error.placeholder.";
    public const string VariableCountKey = "wiredfurni.error.invalid_variable_count";

    private static readonly FrozenSet<string> Boxes = new[]
    {
        "wf_xtra_execution_limit", "wf_xtra_unseen", "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users_by_var",
        "wf_xtra_or_eval", "wf_xtra_rotate_to_dir", ScanChest, "wf_act_teleport_to_room", "wf_trg_at_given_time", "wf_trg_period_long",
        "wf_trg_user_performs_action", "wf_cnd_user_performs_action", "wf_cnd_not_user_performs_action", "wf_act_match_to_sshot",
        "wf_cnd_chest_has_items", "wf_cnd_chest_has_item_type", TextVariable, TextFurniName
    }.ToFrozenSet();

    // Boxes with one picked variable (or two for the filters by variable): a save with no variable takes the empty slots.
    private static readonly FrozenSet<string> VariableBoxes = new[]
    {
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users_by_var", ScanChest, TextVariable, "wf_act_init_transaction", "wf_cnd_chest_has_items", "wf_cnd_chest_has_item_type"
    }.ToFrozenSet();

    // Boxes that answer a user source they do not offer with its own key.
    private static readonly FrozenSet<string> UserSourceKeyed = new[]
    {
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users_by_var", "wf_xtra_rotate_to_dir",
        "wf_act_teleport_to_room", "wf_cnd_user_performs_action", "wf_cnd_not_user_performs_action", TextVariable,
        "wf_cnd_chest_has_items", "wf_cnd_chest_has_item_type"
    }.ToFrozenSet();

    // The action codes the hotel keeps: the ones the client offers, the unoffered 9 and the client expression 67.
    private static bool IsStoredAction(int code) => code is >= 0 and <= 11 or 67;

    public static bool Applies(string name) => Boxes.Contains(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var ints = Fit(native.OwnedIntParams, defaults).ToBuilder();
        var text = native.Text;
        var quantifier = native.Quantifier;

        switch (name) {
            case "wf_xtra_execution_limit":
                ints[0] = Math.Clamp(ints[0], 1, 100);
                ints[1] = Math.Clamp(ints[1], 1, 120);
                text = "";
                break;
            case "wf_xtra_unseen":
                text = "";
                break;
            case "wf_xtra_filter_furni":
                ints[0] = Math.Clamp(ints[0], 1, 1000);
                ints[1] = ints[1] == 2 ? 1 : ints[1];
                ints[2] = Target(ints[2]);
                break;
            case "wf_xtra_filter_furni_by_var":
            case "wf_xtra_filter_users_by_var":
                ints[0] = Math.Clamp(ints[0], 1, 1000);
                ints[1] = ints[1] is >= 0 and <= 5 ? ints[1] : 0;
                ints[2] = ints[2] == 2 ? 1 : ints[2];
                ints[3] = Target(ints[3]);
                break;
            case "wf_xtra_or_eval":
                ints[0] = Math.Clamp(ints[0], -1, 3);
                break;
            case "wf_xtra_rotate_to_dir":
                ints[0] = ints[0] == 2 ? 1 : ints[0];
                ints[1] = ints[1] is >= 0 and <= 3 ? ints[1] : 0;
                ints[4] = Math.Clamp(ints[4], 1, 100000);
                ints[5] = Target(ints[5]);
                ints[9] = Math.Clamp(ints[9], 0, 100000);
                ints[10] = Math.Clamp(ints[10], 0, 7);
                ints[14] = Math.Clamp(ints[14], 0, 2);
                ints[16] = Math.Clamp(ints[16], -64, 64);
                ints[17] = Target(ints[17]);
                ints[18] = Math.Clamp(ints[18], -1000, 1000);
                break;
            // Observed on AIR WIN63-202610201032-107964540: Match To Snapshot switches an option on only for the exact value 1.
            case "wf_act_match_to_sshot":
                for (var index = 0; index < ints.Count; index++) {
                    ints[index] = ints[index] == 1 ? 1 : 0;
                }

                break;
            // Observed on AIR WIN63-202610201032-107964540: the chest conditions clamp the amount to 0..1000000, the option to 0..1, reset
            // an unoffered target to 0 and reset a comparison above 5 or below 0 to 0.
            case "wf_cnd_chest_has_items":
            case "wf_cnd_chest_has_item_type":
                ints[0] = Math.Clamp(ints[0], 0, 1000000);
                ints[1] = Math.Clamp(ints[1], 0, 1);
                ints[2] = Target(ints[2]);
                ints[3] = ints[3] is >= 0 and <= 5 ? ints[3] : 0;
                // Observed on AIR WIN63-202610201032-107964540: a quantifier outside 0 to 2 is stored as 0.
                quantifier = quantifier is { } value ? value is 1 or 2 ? value : 0 : null;
                break;
            // Observed on AIR WIN63-202610201032-107964540: the text output boxes store any non-zero flag as 1, and a target the editor
            // does not offer as 0.
            case TextVariable:
                ints[0] = ints[0] == 0 ? 0 : 1;
                ints[1] = Target(ints[1]);
                ints[2] = ints[2] == 0 ? 0 : 1;
                break;
            case TextFurniName:
                ints[0] = ints[0] == 0 ? 0 : 1;
                break;
            case ScanChest:
                ints[0] = ints[0] == 0 ? 0 : 1;
                text = "";
                break;
            case "wf_act_teleport_to_room":
                // The box has no text field. A text that is not a room id is dropped; a room id is the extension the runtime reads.
                text = uint.TryParse(text, out var roomId) && roomId is not 0 and <= int.MaxValue ? text : "";
                break;
            case "wf_trg_at_given_time":
                ints[0] = Math.Max(ints[0], 0);
                text = "";
                break;
            case "wf_trg_period_long":
                ints[0] = Math.Max(ints[0], 1);
                text = "";
                break;
            default:
                // The user action boxes keep any filter text. An action code outside the stored set reads as 0.
                ints[0] = IsStoredAction(ints[0]) ? ints[0] : 0;
                break;
        }

        return native with { OwnedIntParams = name is "wf_xtra_unseen" or "wf_act_teleport_to_room" ? [] : ints.ToImmutable(), Text = text, Quantifier = quantifier };
    }

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: a save that names fewer variables than the box has slots keeps the empty slot for
    /// each one it leaves out.
    /// </summary>
    public static WiredNativeEditorConfiguration PadVariables(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata) =>
        VariableBoxes.Contains(name) && !native.VariableIds.IsDefault && native.VariableIds.Length < metadata.VariableDefaults.Length
            ? native with { VariableIds = [.. native.VariableIds, .. metadata.VariableDefaults.Skip(native.VariableIds.Length)] }
            : native;

    /// <summary>
    /// The text of a new Text Output save in its stored form. Observed on AIR WIN63-202610201032-107964540: the name is lower case, a
    /// single placeholder keeps the name only and a placeholder for several values keeps the name and the delimiter. Text after a second
    /// tab is cut. A stored row is read as it is.
    /// </summary>
    public static WiredNativeEditorConfiguration NormalizeNewSave(string name, WiredNativeEditorConfiguration native)
    {
        if (name is not (TextVariable or TextFurniName) || native.OwnedIntParams.IsDefaultOrEmpty) {
            return native;
        }

        var parts = native.Text.Split('\t');
        var placeholder = parts[0].ToLowerInvariant();

        return native with { Text = native.OwnedIntParams[0] == 0 ? placeholder : placeholder + "\t" + (parts.Length > 1 ? parts[1] : "") };
    }

    /// <summary>
    /// The key of a Text Output name or delimiter the hotel refuses on a new save, or null. Observed on AIR WIN63-202610201032-107964540:
    /// the name has 1 to 40 characters of a-z, 0-9 and _ in lower case (capitals are lowered first) and is not "trigger_text"; the
    /// delimiter of a placeholder for several values has 1 to 5 characters.
    /// </summary>
    public static string? PlaceholderRefusal(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata)
    {
        if (name is not (TextVariable or TextFurniName) || native.OwnedIntParams.IsDefaultOrEmpty) {
            return null;
        }

        // A box left on its frame (no name, the default ints, no variable) is not a save: a new box starts there.
        if (native.Text.Length == 0 && native.OwnedIntParams.SequenceEqual(metadata.OwnedDefaults)
            && native.VariableIds.All(Plus.HabboHotel.Items.Wired.Variables.WiredVariableAbsent.Is)) {
            return null;
        }

        var parts = native.Text.Split('\t');
        var placeholder = parts[0].ToLowerInvariant();

        if (placeholder.Length is 0 or > 40) {
            return PlaceholderKey + "name_length";
        }

        if (!placeholder.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_')) {
            return PlaceholderKey + "name_syntax";
        }

        if (placeholder == "trigger_text") {
            return PlaceholderKey + "reserved";
        }

        if (native.OwnedIntParams[0] == 0) {
            return null;
        }

        var delimiter = parts.Length > 1 ? parts[1] : "";

        return delimiter.Length == 0 ? PlaceholderKey + "delimiter_empty" : delimiter.Length > 5 ? PlaceholderKey + "delimiter_length" : null;
    }

    /// <summary>The key of a source the hotel refuses with its own wording, or null.</summary>
    public static string? SourceRefusal(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata)
    {
        if (VariableBoxes.Contains(name) && !native.VariableIds.IsDefault && native.VariableIds.Length > metadata.VariableDefaults.Length) {
            return VariableCountKey;
        }

        if (PlaceholderRefusal(name, Normalized(name, native, metadata), metadata) is { } placeholder) {
            return placeholder;
        }

        if (UserSourceKeyed.Contains(name)
            && native.UserSourceTypes.Where((source, index) => index < metadata.UsersAllowed.Length
                && !metadata.UsersAllowed[index].Contains(source) && !WiredNativeEditorProjection.AnyTriggerContext.Contains(source)).Any()) {
            return UserSourceKey;
        }

        return ChestSlotRefused(name, native) ? ChestSourceKey : null;
    }

    /// <summary>Scan Chest takes the item types from either pick list, but its chests only from the first.</summary>
    public static bool ChestSlotRefused(string name, WiredNativeEditorConfiguration native) =>
        name == ScanChest && native.FurniSourceTypes is [_, 101];

    /// <summary>A save the hotel answers with nothing: Scan Chest with the source value 1 for its item types.</summary>
    public static bool DroppedWithoutReply(string name, WiredNativeEditorConfiguration native) =>
        name == ScanChest && native.FurniSourceTypes is [1, ..];

    private static WiredNativeEditorConfiguration Normalized(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata) =>
        Applies(name) ? Normalize(name, native, metadata.OwnedDefaults) : native;

    // Missing ints take the frame default, extra ints are dropped.
    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];

    // An AIR reference target the editor does not offer reads as 0.
    private static int Target(int value) => value is 0 or 1 or -10 or -20 ? value : 0;
}
