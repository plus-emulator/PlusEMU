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
        or "wf_act_move_furni_as_group" or "wf_act_neg_send_signal" or "wf_cnd_slc_quantity" or "wf_cnd_team_has_rank" or "wf_cnd_team_has_score"
        or "wf_cnd_time_less_than" or "wf_cnd_time_more_than" or "wf_cnd_trggrer_on_frn" or "wf_cnd_actor_in_group" or "wf_cnd_not_in_group"
        or "wf_cnd_stuff_is" or "wf_cnd_not_stuff_is" or "wf_cnd_has_handitem" or "wf_cnd_not_has_handitem";

    // These conditions keep a quantifier of 2 as sent, also the ones whose editor shows no quantifier.
    public static bool KeepsQuantifierTwo(string name) => name is "wf_cnd_slc_quantity" or "wf_cnd_team_has_rank" or "wf_cnd_team_has_score"
        or "wf_cnd_time_less_than" or "wf_cnd_time_more_than" or "wf_cnd_trggrer_on_frn" or "wf_cnd_actor_in_group" or "wf_cnd_not_in_group"
        or "wf_cnd_stuff_is" or "wf_cnd_not_stuff_is" or "wf_cnd_has_handitem" or "wf_cnd_not_has_handitem";

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
        var p = name is "wf_cnd_stuff_is" or "wf_cnd_not_stuff_is" ? native.OwnedIntParams : Fit(native.OwnedIntParams, defaults);

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
            // Selection Quantity: the user or furni flag is 0 or 1, the amount 0..100, and the comparison keeps 3 and 4.
            case "wf_cnd_slc_quantity":
                p = [p[0] is 0 or 1 ? p[0] : 0, Math.Clamp(p[1], 0, 100), Math.Max(p[2], 0)];
                break;
            case "wf_cnd_team_has_rank":
                p = [Math.Clamp(p[0], 0, 4), Math.Clamp(p[1], 0, 3)];
                break;
            case "wf_cnd_team_has_score":
                p = [Math.Clamp(p[0], 0, 4), Math.Clamp(p[1], 0, 1000), Math.Max(p[2], 0)];
                break;
            // The time conditions keep any threshold, negative ones too.
            case "wf_cnd_time_less_than":
            case "wf_cnd_time_more_than":
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

        // The group conditions keep the id text only when it is a decimal number.
        if (name is "wf_cnd_actor_in_group" or "wf_cnd_not_in_group" && !IsDecimalNumber(text)) {
            text = "";
        }

        return native with { OwnedIntParams = p, Text = text, Quantifier = StoredQuantifier(name, native.Quantifier) };
    }

    private static int? StoredQuantifier(string name, int? quantifier) => KeepsQuantifierTwo(name) && quantifier is { } value
        ? value is 1 or 2 ? value : 0 : quantifier;

    /// <summary>An optional minus sign and digits that fit a 64-bit number.</summary>
    internal static bool IsDecimalNumber(string text) => text.Length == 0
        || long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)
        || text[0] == '-' && long.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static int Flag(int value) => value == 0 ? 0 : 1;

    // Missing ints take the frame default, extra ints are dropped.
    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];
}
