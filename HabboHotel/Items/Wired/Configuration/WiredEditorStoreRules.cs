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
    public const string ChestSourceKey = "wiredfurni.error.require_furni_chests";
    private const string ScanChest = "wf_xtra_scan_chest_furni_by_type";
    private const string TextVariable = "wf_xtra_text_output_variable";
    private const string TextFurniName = "wf_xtra_text_output_furni_name";
    private const string PlaceholderKey = "wiredfurni.error.placeholder.";
    public const string VariableCountKey = "wiredfurni.error.invalid_variable_count";
    private const string BotReachedFurni = "wf_trg_bot_reached_stf";
    private const int KickMessageLength = 200;

    /// <param name="Store">The stored form of a save, or null for a box that only has a variable or user source check here.</param>
    /// <param name="VariableSlots">The box has a fixed number of variable slots: a missing slot takes the empty variable and an extra
    /// variable is refused with the count key.</param>
    private sealed record Rule(Action<WiredStoreDraft>? Store = null, bool VariableSlots = false);

    // Each box is named once, with its stored form and flags. The ints are cut and padded to the frame before the rule runs.
    private static readonly FrozenDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        { "wf_xtra_execution_limit", new(ExecutionLimit) },
        { "wf_xtra_unseen", new(Unseen) },
        { "wf_xtra_filter_furni", new(FilterFurni, VariableSlots: true) },
        { "wf_xtra_filter_furni_by_var", new(FilterByVariable, VariableSlots: true) },
        { "wf_xtra_filter_users_by_var", new(FilterByVariable, VariableSlots: true) },
        { "wf_xtra_filter_users", new(FilterUsers, VariableSlots: true) },
        { "wf_act_teleport_to", new(FlagWithoutText) },
        { "wf_act_kick_user", new(KickUser) },
        { "wf_xtra_or_eval", new(draft => draft.Ints[0] = Math.Clamp(draft.Ints[0], -1, 3)) },
        { "wf_xtra_rotate_to_dir", new(RotateToDirection) },
        { ScanChest, new(Scan, VariableSlots: true) },
        { "wf_act_teleport_to_room", new(TeleportToRoom) },
        { "wf_trg_at_given_time", new(AtLeast(0)) },
        { "wf_trg_period_long", new(AtLeast(1)) },
        // The user action boxes keep any filter text. An action code outside the stored set reads as 0.
        { "wf_trg_user_performs_action", new(UserAction) },
        { "wf_cnd_user_performs_action", new(UserAction) },
        { "wf_cnd_not_user_performs_action", new(UserAction) },
        { "wf_act_match_to_sshot", new(MatchToSnapshot) },
        { "wf_cnd_has_var", new(HasVariable, VariableSlots: true) },
        { "wf_cnd_neg_has_var", new(HasVariable, VariableSlots: true) },
        { "wf_cnd_chest_has_items", new(ChestCondition, VariableSlots: true) },
        { "wf_cnd_chest_has_item_type", new(ChestCondition, VariableSlots: true) },
        { TextVariable, new(TextOutputVariable, VariableSlots: true) },
        { TextFurniName, new(draft => draft.Ints[0] = draft.Ints[0] == 0 ? 0 : 1) },
        // Observed on AIR WIN63-202610201032-107964540: the bot name is kept as sent, of any length.
        { BotReachedFurni, new(_ => { }) },
        // Only the variable slots are checked. The transaction triggers have none.
        { "wf_act_init_transaction", new(VariableSlots: true) },
        { "wf_trg_transaction_complete", new(VariableSlots: true) },
        { "wf_trg_transaction_fail", new(VariableSlots: true) },
        // Observed on AIR WIN63-202610201032-107964540: the room triggers have no variable slot either, so a variable gets the count key.
        { "wf_trg_enter_room", new(VariableSlots: true) },
        { "wf_trg_leave_room", new(VariableSlots: true) },
        // Observed on AIR WIN63-202610201032-107964540: any non-zero match criteria is stored as 1, and the box has no text.
        { "wf_act_cancel_transaction", new(FlagWithoutText, VariableSlots: true) }
    }.ToFrozenDictionary();

    // The action codes the hotel keeps: the ones the client offers, the unoffered 9 and the client expression 67.
    private static bool IsStoredAction(int code) => code is >= 0 and <= 11 or 67;

    public static bool Applies(string name) => Rules.TryGetValue(name, out var rule) && rule.Store is not null;

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        if (!Rules.TryGetValue(name, out var rule) || rule.Store is not { } store) {
            return native;
        }

        var draft = new WiredStoreDraft(native, defaults);
        store(draft);

        return draft.ToNative();
    }

    private static void ExecutionLimit(WiredStoreDraft draft)
    {
        draft.Ints[0] = Math.Clamp(draft.Ints[0], 1, 100);
        draft.Ints[1] = Math.Clamp(draft.Ints[1], 1, 120);
        draft.Text = "";
    }

    // The first int has a lower bound and the box has no text.
    private static Action<WiredStoreDraft> AtLeast(int minimum) => draft =>
    {
        draft.Ints[0] = Math.Max(draft.Ints[0], minimum);
        draft.Text = "";
    };

    private static void Unseen(WiredStoreDraft draft)
    {
        draft.Ints.Clear();
        draft.Text = "";
    }

    private static void FilterFurni(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

        ints[0] = Math.Clamp(ints[0], 1, 1000);
        ints[1] = ints[1] == 2 ? 1 : ints[1];
        ints[2] = Target(ints[2]);
    }

    // Observed on AIR WIN63-202610201032-107964540: the amount is held to 1..1000, the operand kind to 0..1 and the reference target to
    // the four offered values. The box has no text.
    private static void FilterUsers(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

        ints[0] = Math.Clamp(ints[0], 1, 1000);
        ints[1] = Math.Clamp(ints[1], 0, 1);
        ints[2] = Target(ints[2]);
        draft.Text = "";
    }

    // Observed on AIR WIN63-202610201032-107964540: the flag (Teleport To's fast flag, Cancel Transaction's match criteria) is 1 for any
    // value but 0, and the box has no text.
    private static void FlagWithoutText(WiredStoreDraft draft)
    {
        draft.Ints[0] = draft.Ints[0] == 0 ? 0 : 1;
        draft.Text = "";
    }

    // Observed on AIR WIN63-202610201032-107964540: the message is cut at 200 characters and a tab or a line feed in it is stored as a
    // space.
    private static void KickUser(WiredStoreDraft draft)
    {
        var text = draft.Text.Replace('\t', ' ').Replace('\n', ' ');
        draft.Text = text.Length > KickMessageLength ? text[..KickMessageLength] : text;
    }

    private static void FilterByVariable(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

        ints[0] = Math.Clamp(ints[0], 1, 1000);
        ints[1] = ints[1] is >= 0 and <= 5 ? ints[1] : 0;
        ints[2] = ints[2] == 2 ? 1 : ints[2];
        ints[3] = Target(ints[3]);
    }

    private static void RotateToDirection(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

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
    }

    // Observed on AIR WIN63-202610201032-107964540: Match To Snapshot switches an option on only for the exact value 1.
    private static void MatchToSnapshot(WiredStoreDraft draft)
    {
        for (var index = 0; index < draft.Ints.Count; index++) {
            draft.Ints[index] = draft.Ints[index] == 1 ? 1 : 0;
        }
    }

    // Observed on AIR WIN63-202610201032-107964540: the chest conditions clamp the amount to 0..1000000, the option to 0..1, reset
    // an unoffered target to 0 and reset a comparison above 5 or below 0 to 0.
    private static void ChestCondition(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

        ints[0] = Math.Clamp(ints[0], 0, 1000000);
        ints[1] = Math.Clamp(ints[1], 0, 1);
        ints[2] = Target(ints[2]);
        ints[3] = ints[3] is >= 0 and <= 5 ? ints[3] : 0;
        // Observed on AIR WIN63-202610201032-107964540: a quantifier outside 0 to 2 is stored as 0.
        draft.Quantifier = draft.Quantifier is { } value ? value is 1 or 2 ? value : 0 : null;
    }

    // Observed on AIR WIN63-202610201032-107964540: Has Variable stores a target the editor does not offer as 0, and has no text field.
    private static void HasVariable(WiredStoreDraft draft)
    {
        draft.Ints[0] = Target(draft.Ints[0]);
        draft.Text = "";
    }

    // Observed on AIR WIN63-202610201032-107964540: the text output boxes store any non-zero flag as 1, and a target the editor
    // does not offer as 0.
    private static void TextOutputVariable(WiredStoreDraft draft)
    {
        var ints = draft.Ints;

        ints[0] = ints[0] == 0 ? 0 : 1;
        ints[1] = Target(ints[1]);
        ints[2] = ints[2] == 0 ? 0 : 1;
    }

    private static void Scan(WiredStoreDraft draft)
    {
        draft.Ints[0] = draft.Ints[0] == 0 ? 0 : 1;
        draft.Text = "";
    }

    // The box has no text field. A text that is not a room id is dropped; a room id is the extension the runtime reads.
    private static void TeleportToRoom(WiredStoreDraft draft)
    {
        draft.Ints.Clear();
        draft.Text = uint.TryParse(draft.Text, out var roomId) && roomId is not 0 and <= int.MaxValue ? draft.Text : "";
    }

    private static void UserAction(WiredStoreDraft draft) => draft.Ints[0] = IsStoredAction(draft.Ints[0]) ? draft.Ints[0] : 0;

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: a save that names fewer variables than the box has slots keeps the empty slot for
    /// each one it leaves out.
    /// </summary>
    public static WiredNativeEditorConfiguration PadVariables(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata) =>
        Rules.TryGetValue(name, out var rule) && rule.VariableSlots && !native.VariableIds.IsDefault && native.VariableIds.Length < metadata.VariableDefaults.Length
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
        var rule = Rules.GetValueOrDefault(name);

        if (rule is { VariableSlots: true } && !native.VariableIds.IsDefault && native.VariableIds.Length > metadata.VariableDefaults.Length) {
            return VariableCountKey;
        }

        if (PlaceholderRefusal(name, Normalized(name, native, metadata), metadata) is { } placeholder) {
            return placeholder;
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

    // An AIR reference target the editor does not offer reads as 0.
    private static int Target(int value) => value is 0 or 1 or -10 or -20 ? value : 0;
}
