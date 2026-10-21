using System.Collections.Frozen;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// What a save may carry that the box does not read. Observed on AIR WIN63-202610201032-107964540: the hotel answers such a save and
/// stores the box's own fields only. A text the box has no field for is dropped, ints beyond the box's fields are dropped, and ints
/// the save leaves out take the frame default. Each box below was saved this way and read back.
/// </summary>
internal static class WiredSaveLeniency
{
    private const string FurniNameOutput = "wf_xtra_text_output_furni_name";

    private enum IntsMode
    {
        /// <summary>The ints are cut and padded by the box's own store rule.</summary>
        Keep,
        /// <summary>The box has no int fields: all ints are dropped.</summary>
        None,
        /// <summary>The show-multiple flag reads 1 for any value but 0, and 0 when the save leaves it out.</summary>
        ShowMultipleFlag
    }

    /// <param name="NoText">The box has no text field.</param>
    /// <param name="FillsEmptyUserSources">The box reads an empty user source list as the frame default.</param>
    /// <param name="PadsUserSources">The box keeps the frame default for each user source slot a save leaves out.</param>
    private sealed record Rule(bool NoText = false, IntsMode Ints = IntsMode.Keep, bool FillsEmptyUserSources = false, bool PadsUserSources = false);

    private static readonly Rule Bare = new(NoText: true, Ints: IntsMode.None);
    private static readonly Rule Textless = new(NoText: true);
    private static readonly Rule TextlessSources = new(NoText: true, FillsEmptyUserSources: true);
    private static readonly Rule IntlessSources = new(Ints: IntsMode.None, FillsEmptyUserSources: true);
    private static readonly Rule PaddedSources = new(PadsUserSources: true);

    // Each box is named once, with what its save drops.
    private static readonly FrozenDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        // Boxes without a text field or int fields.
        { "wf_act_chase", Bare },
        { "wf_act_flee", Bare },
        { "wf_act_neg_call_stacks", Bare },
        { "wf_act_toggle_to_rnd", Bare },
        { "wf_act_reset_timers", Bare },
        { "wf_act_unfreeze", Bare },
        { "wf_cnd_stuff_is", Bare },
        { "wf_cnd_not_stuff_is", Bare },
        { "wf_cnd_trggrer_on_frn", Bare },
        { "wf_cnd_not_trggrer_on", Bare },
        { "wf_trg_collision", Bare },
        { "wf_trg_state_changed", Bare },
        { "wf_trg_walks_off_furni", Bare },
        { "wf_trg_click_tile", Bare },
        { "wf_slc_users_onfurni", Bare },
        { "wf_slc_furni_signal", Bare },
        // Boxes without a text field. The ints of these boxes are cut and padded by their own store rules.
        { "wf_cnd_actor_dir", Textless },
        { "wf_cnd_actor_in_team", Textless },
        { "wf_cnd_not_in_team", Textless },
        { "wf_cnd_counter_time_matches", Textless },
        { "wf_cnd_furnis_hv_avtrs", Textless },
        { "wf_cnd_not_furni_on", Textless },
        { "wf_cnd_not_hv_avtrs", Textless },
        { "wf_cnd_has_altitude", Textless },
        { "wf_cnd_not_user_count", Textless },
        { "wf_cnd_user_count_in", Textless },
        { "wf_cnd_slc_quantity", Textless },
        { "wf_cnd_team_has_rank", Textless },
        { "wf_cnd_team_has_score", Textless },
        { "wf_cnd_time_less_than", Textless },
        { "wf_cnd_time_more_than", Textless },
        { "wf_cnd_has_handitem", Textless },
        { "wf_cnd_not_has_handitem", Textless },
        { "wf_trg_click_user", Textless },
        { "wf_trg_clock_counter", Textless },
        { "wf_slc_furni_altitude", Textless },
        { "wf_slc_furni_area", Textless },
        { "wf_slc_furni_bytype", Textless },
        { "wf_slc_furni_neighborhood", Textless },
        { "wf_slc_remote", Textless },
        { "wf_slc_users_bytype", Textless },
        { "wf_slc_users_handitem", Textless },
        { "wf_trg_stuff_state", Textless },
        { "wf_cnd_chest_has_items", Textless },
        { "wf_cnd_chest_has_item_type", Textless },
        // Boxes that also read an empty user source list as the frame default.
        { "wf_act_move_furni_as_group", TextlessSources },
        { "wf_act_neg_send_signal", TextlessSources },
        { "wf_trg_bot_reached_avtr", IntlessSources },
        { "wf_trg_bot_reached_stf", IntlessSources },
        // Triggerer Matches keeps its default comparison slot when a save names fewer user sources.
        { "wf_cnd_triggerer_match", PaddedSources },
        { "wf_cnd_not_triggerer_match", PaddedSources },
        { FurniNameOutput, new(Ints: IntsMode.ShowMultipleFlag) }
    }.ToFrozenDictionary();

    public static bool FillsEmptyUserSources(string name) => Rules.TryGetValue(name, out var rule) && rule.FillsEmptyUserSources;

    public static bool PadsUserSources(string name) => Rules.TryGetValue(name, out var rule) && rule.PadsUserSources;

    public static bool Applies(string name) => Rules.ContainsKey(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native) =>
        Rules.TryGetValue(name, out var rule)
            ? native with
            {
                Text = rule.NoText ? "" : native.Text,
                OwnedIntParams = rule.Ints switch
                {
                    IntsMode.None => [],
                    IntsMode.ShowMultipleFlag => [native.OwnedIntParams is [var flag, ..] && flag != 0 ? 1 : 0],
                    _ => native.OwnedIntParams
                }
            }
            : native;
}
