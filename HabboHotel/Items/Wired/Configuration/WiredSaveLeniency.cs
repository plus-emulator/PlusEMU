using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// What a save may carry that the box does not read. Observed on AIR WIN63-202610201032-107964540: the hotel answers such a save and
/// stores the box's own fields only. A text the box has no field for is dropped, ints beyond the box's fields are dropped, and ints
/// the save leaves out take the frame default. Each box below was saved this way and read back.
/// </summary>
internal static class WiredSaveLeniency
{
    // Boxes without a text field.
    private static readonly FrozenSet<string> NoText = new[]
    {
        "wf_act_chase", "wf_act_flee", "wf_act_neg_call_stacks", "wf_act_toggle_to_rnd", "wf_act_reset_timers", "wf_act_unfreeze",
        "wf_act_move_furni_as_group", "wf_act_neg_send_signal",
        "wf_cnd_stuff_is", "wf_cnd_not_stuff_is", "wf_cnd_trggrer_on_frn", "wf_cnd_not_trggrer_on", "wf_cnd_actor_dir", "wf_cnd_actor_in_team",
        "wf_cnd_not_in_team", "wf_cnd_counter_time_matches", "wf_cnd_furnis_hv_avtrs", "wf_cnd_not_furni_on", "wf_cnd_not_hv_avtrs",
        "wf_cnd_has_altitude", "wf_cnd_not_user_count", "wf_cnd_slc_quantity", "wf_cnd_team_has_rank", "wf_cnd_team_has_score",
        "wf_cnd_time_less_than", "wf_cnd_time_more_than", "wf_cnd_has_handitem", "wf_cnd_not_has_handitem",
        "wf_trg_collision", "wf_trg_state_changed", "wf_trg_walks_off_furni", "wf_trg_click_tile", "wf_trg_click_user", "wf_trg_clock_counter",
        "wf_slc_users_onfurni", "wf_slc_furni_signal"
    }.ToFrozenSet();

    // Boxes without int fields, whose ints are all dropped. The ints of the other boxes are cut and padded by their own store rules.
    private static readonly FrozenSet<string> NoInts = new[]
    {
        "wf_act_chase", "wf_act_flee", "wf_act_neg_call_stacks", "wf_act_toggle_to_rnd", "wf_act_reset_timers", "wf_act_unfreeze",
        "wf_cnd_stuff_is", "wf_cnd_not_stuff_is", "wf_cnd_trggrer_on_frn", "wf_cnd_not_trggrer_on",
        "wf_trg_collision", "wf_trg_state_changed", "wf_trg_walks_off_furni", "wf_trg_click_tile", "wf_slc_users_onfurni", "wf_slc_furni_signal"
    }.ToFrozenSet();

    public static bool Applies(string name) => NoText.Contains(name) || NoInts.Contains(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native) => native with
    {
        Text = NoText.Contains(name) ? "" : native.Text,
        OwnedIntParams = NoInts.Contains(name) ? [] : native.OwnedIntParams
    };
}
