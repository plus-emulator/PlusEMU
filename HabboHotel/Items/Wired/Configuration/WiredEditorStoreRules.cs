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

    private static readonly FrozenSet<string> Boxes = new[]
    {
        "wf_xtra_execution_limit", "wf_xtra_unseen", "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users_by_var",
        "wf_xtra_or_eval", "wf_xtra_rotate_to_dir", ScanChest, "wf_act_teleport_to_room", "wf_trg_at_given_time", "wf_trg_period_long",
        "wf_trg_user_performs_action", "wf_cnd_user_performs_action", "wf_cnd_not_user_performs_action", "wf_act_match_to_sshot",
        "wf_cnd_chest_has_items", "wf_cnd_chest_has_item_type"
    }.ToFrozenSet();

    // Boxes that answer a user source they do not offer with its own key.
    private static readonly FrozenSet<string> UserSourceKeyed = new[]
    {
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users_by_var", "wf_xtra_rotate_to_dir",
        "wf_act_teleport_to_room", "wf_cnd_user_performs_action", "wf_cnd_not_user_performs_action"
    }.ToFrozenSet();

    // The action codes the hotel keeps: the ones the client offers, the unoffered 9 and the client expression 67.
    private static bool IsStoredAction(int code) => code is >= 0 and <= 11 or 67;

    public static bool Applies(string name) => Boxes.Contains(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var ints = Fit(native.OwnedIntParams, defaults).ToBuilder();
        var text = native.Text;

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

        return native with { OwnedIntParams = name is "wf_xtra_unseen" or "wf_act_teleport_to_room" ? [] : ints.ToImmutable(), Text = text };
    }

    /// <summary>The key of a source the hotel refuses with its own wording, or null.</summary>
    public static string? SourceRefusal(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata)
    {
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

    // Missing ints take the frame default, extra ints are dropped.
    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];

    // An AIR reference target the editor does not offer reads as 0.
    private static int Target(int value) => value is 0 or 1 or -10 or -20 ? value : 0;
}
