using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these boxes. A save with an odd value is not refused: the value is clamped, switched to the
/// nearest flag value, or filled from the frame default, and the box reads back the stored form.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
internal static class WiredConditionStoreRules
{
    public static bool Applies(string name) => name is "wf_cnd_actor_dir" or "wf_cnd_actor_in_team" or "wf_cnd_not_in_team"
        or "wf_cnd_counter_time_matches" or "wf_cnd_furnis_hv_avtrs" or "wf_cnd_not_furni_on" or "wf_cnd_not_hv_avtrs"
        or "wf_cnd_has_altitude" or "wf_cnd_match_snapshot" or "wf_cnd_not_match_snap" or "wf_cnd_not_user_count" or "wf_cnd_match_date" or "wf_cnd_match_time"
        or "wf_act_move_furni_as_group" or "wf_act_neg_send_signal";

    public const string UserSourceKey = "wiredfurni.error.invalid_user_source";

    private static readonly HashSet<string> UserSourceKeyed = ["wf_cnd_actor_dir", "wf_cnd_actor_in_team", "wf_cnd_not_in_team",
        "wf_cnd_not_trggrer_on", "wf_act_unfreeze", "wf_act_move_furni_as_group", "wf_act_neg_send_signal"];

    /// <summary>The key of a user source these boxes refuse with their own wording, or null. Sources 10 and 11 come from the trigger.</summary>
    public static string? UserSourceRefusal(string name, WiredNativeEditorConfiguration native, WiredNativeEditorMetadata metadata) =>
        UserSourceKeyed.Contains(name) && native.UserSourceTypes.Where((source, index) => index < metadata.UsersAllowed.Length
            && !metadata.UsersAllowed[index].Contains(source) && !WiredNativeEditorProjection.AnyTriggerContext.Contains(source)).Any()
            ? UserSourceKey : null;

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var p = Fit(native.OwnedIntParams, defaults);

        switch (name) {
            case "wf_cnd_actor_dir":
                p = [Math.Clamp(p[0], 0, 255)];
                break;
            case "wf_cnd_actor_in_team":
            case "wf_cnd_not_in_team":
                p = [Math.Clamp(p[0], 0, 4)];
                break;
            // Only the comparison keeps values the editor never offers: 3 is stored (the upper end of the comparison is not clamped).
            case "wf_cnd_counter_time_matches":
                p = [Math.Clamp(p[0], 0, 59), Math.Clamp(p[1], 0, 99), Math.Clamp(p[2], 0, 1), Math.Max(p[3], 0)];
                break;
            case "wf_cnd_furnis_hv_avtrs":
            case "wf_cnd_not_furni_on":
            case "wf_cnd_not_hv_avtrs":
                p = [p[0] == 0 ? 0 : 1];
                break;
            case "wf_cnd_has_altitude":
                p = [Math.Clamp(p[0], 0, 8000), Math.Max(p[1], 0)];
                break;
            case "wf_cnd_match_snapshot":
            case "wf_cnd_not_match_snap":
                p = [.. p.Select(flag => flag == 1 ? 1 : 0)];
                break;
            case "wf_cnd_not_user_count":
                var minimum = Math.Clamp(p[0], 0, 125);
                p = [minimum, Math.Max(Math.Clamp(p[1], 0, 125), minimum)];
                break;
            // The first flag of each box switches a field on: any value but 0 is 1. Ranges keep the order they were sent in, except that a
            // year range that ends before it starts has its end raised to the start (9999..0 is stored 9999..9999).
            case "wf_cnd_match_date":
                var yearStart = Math.Clamp(p[6], 0, 9999);
                p = [Flag(p[0]), Flag(p[1]), Math.Clamp(p[2], 0, 127), Math.Clamp(p[3], 1, 31), Math.Clamp(p[4], 1, 31),
                    Math.Clamp(p[5], 0, 4095), yearStart, Math.Max(Math.Clamp(p[7], 0, 9999), yearStart)];
                break;
            // Move Furni As Group: the target flag is 0 or 1 and each offset is clamped to the editor's -64..64.
            case "wf_act_move_furni_as_group":
                p = [Flag(p[0]), Math.Clamp(p[1], -64, 64), Math.Clamp(p[2], -64, 64)];
                break;
            // The two split flags of a signal count only when they are exactly 1.
            case "wf_act_neg_send_signal":
                p = [.. p.Select(flag => flag == 1 ? 1 : 0)];
                break;
            case "wf_cnd_match_time":
                p = [Flag(p[0]), Flag(p[1]), Flag(p[2]), .. p.Skip(3).Take(4).Select(value => Math.Clamp(value, 0, 59)),
                    .. p.Skip(7).Take(2).Select(value => Math.Clamp(value, 0, 23))];
                break;
        }

        var text = native.Text;

        if (name is "wf_cnd_match_date" or "wf_cnd_match_time" && !Modern.Conditions.WiredTimeConditions.IsValidZone(text)) {
            text = "";
        }

        return native with { OwnedIntParams = p, Text = text };
    }

    private static int Flag(int value) => value == 0 ? 0 : 1;

    // Missing ints take the frame default, extra ints are dropped.
    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];
}
