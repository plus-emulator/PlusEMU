using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these conditions. A save with an odd value is not refused: the value is clamped, switched to the
/// nearest flag value, or filled from the frame default, and the box reads back the stored form.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
internal static class WiredConditionStoreRules
{
    public static bool Applies(string name) => name is "wf_cnd_actor_dir" or "wf_cnd_actor_in_team" or "wf_cnd_not_in_team"
        or "wf_cnd_counter_time_matches" or "wf_cnd_furnis_hv_avtrs" or "wf_cnd_not_furni_on" or "wf_cnd_not_hv_avtrs"
        or "wf_cnd_has_altitude" or "wf_cnd_match_snapshot" or "wf_cnd_not_match_snap" or "wf_cnd_not_user_count";

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
        }

        return native with { OwnedIntParams = p };
    }

    // Missing ints take the frame default, extra ints are dropped.
    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];
}
