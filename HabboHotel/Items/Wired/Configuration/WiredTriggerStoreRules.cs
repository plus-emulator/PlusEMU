using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these trigger saves: a missing int takes the frame default, an extra int is dropped, and an odd
/// value is reset or clamped instead of refused.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
internal static class WiredTriggerStoreRules
{
    // Each trigger is named once, with the stored form of its ints (already cut and padded to the frame).
    private static readonly FrozenDictionary<string, Func<ImmutableArray<int>, ImmutableArray<int>>> Rules =
        new Dictionary<string, Func<ImmutableArray<int>, ImmutableArray<int>>>
        {
            // Only the exact value 1 is the current-state mode.
            { "wf_trg_stuff_state", p => [p[0] == 1 ? 1 : 0] },
            // Any non-zero flag is stored as 1.
            { "wf_trg_click_user", p => [.. p.Select(flag => flag == 0 ? 0 : 1)] },
            // The room triggers and Transaction Failed have no int fields: a save with ints is accepted and the ints are dropped.
            { "wf_trg_enter_room", p => p },
            { "wf_trg_leave_room", p => p },
            { "wf_trg_transaction_fail", p => p },
            // Seconds, minutes and the half second are clamped to what the editor offers.
            { "wf_trg_clock_counter", p => [Math.Clamp(p[0], 0, 59), Math.Clamp(p[1], 0, 99), Math.Clamp(p[2], 0, 1)] }
        }.ToFrozenDictionary();

    public static bool Applies(string name) => Rules.ContainsKey(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults) =>
        Rules.TryGetValue(name, out var rule) ? native with { OwnedIntParams = rule(WiredStoreDraft.Fit(native.OwnedIntParams, defaults)) } : native;
}
