using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these trigger saves: a missing int takes the frame default, an extra int is dropped, and an odd
/// value is reset or clamped instead of refused.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
internal static class WiredTriggerStoreRules
{
    public static bool Applies(string name) => name is "wf_trg_stuff_state" or "wf_trg_click_user" or "wf_trg_clock_counter";

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var p = Fit(native.OwnedIntParams, defaults);

        p = name switch
        {
            // Only the exact value 1 is the current-state mode.
            "wf_trg_stuff_state" => [p[0] == 1 ? 1 : 0],
            // Any non-zero flag is stored as 1.
            "wf_trg_click_user" => [.. p.Select(flag => flag == 0 ? 0 : 1)],
            // Seconds, minutes and the half second are clamped to what the editor offers.
            _ => [Math.Clamp(p[0], 0, 59), Math.Clamp(p[1], 0, 99), Math.Clamp(p[2], 0, 1)]
        };

        return native with { OwnedIntParams = p };
    }

    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];
}
