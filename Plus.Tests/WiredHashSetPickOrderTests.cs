using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: every card below reopens its picks in the iteration order of a Java HashSet<Integer>
// of the saved ids, not in the saved order. Orders were saved and reopened on Habbo with five floor ids.
public class WiredHashSetPickOrderTests
{
    private static readonly int[] HabboSent = [2147418128, 2147418114, 2147418112, 2147418130, 2147418113];
    private static readonly int[] HabboReopened = [2147418114, 2147418130, 2147418113, 2147418128, 2147418112];

    // Every card below was saved and reopened on Habbo with the same scrambled set; cards that need picks of one furni kind (counters, antennas,
    // links, click tiles, contracts) were saved with furni of that kind.
    private static readonly string[] ProvenCards =
    [
        "wf_act_adjust_clock", "wf_act_bot_clothes", "wf_act_bot_follow_avatar",
        "wf_act_bot_give_handitem", "wf_act_bot_move", "wf_act_bot_talk",
        "wf_act_bot_talk_to_avatar", "wf_act_bot_teleport", "wf_act_call_stacks",
        "wf_act_cancel_transaction", "wf_act_change_var_val", "wf_act_chase",
        "wf_act_click_conf", "wf_act_control_clock", "wf_act_flee",
        "wf_act_freeze", "wf_act_furni_to_furni", "wf_act_furni_to_user",
        "wf_act_give_score", "wf_act_give_score_tm", "wf_act_give_var",
        "wf_act_join_team", "wf_act_kick_user", "wf_act_leave_team",
        "wf_act_log", "wf_act_match_to_sshot", "wf_act_move_furni_as_group",
        "wf_act_move_rotate", "wf_act_move_rotate_user", "wf_act_move_to_dir",
        "wf_act_neg_call_stacks", "wf_act_neg_log", "wf_act_neg_send_signal",
        "wf_act_place_furni", "wf_act_rel_mov", "wf_act_remove_var",
        "wf_act_reset_timers", "wf_act_send_message", "wf_act_send_signal",
        "wf_act_set_altitude", "wf_act_set_highlight", "wf_act_set_visibility",
        "wf_act_show_message", "wf_act_teleport_to", "wf_act_toggle_state",
        "wf_act_toggle_to_rnd", "wf_act_unfreeze", "wf_act_user_to_furni",
        "wf_cnd_actor_dir", "wf_cnd_actor_in_group", "wf_cnd_actor_in_team",
        "wf_cnd_chest_has_item_type", "wf_cnd_counter_time_matches", "wf_cnd_date_rng_active",
        "wf_cnd_furnis_hv_avtrs", "wf_cnd_has_altitude", "wf_cnd_has_furni_on",
        "wf_cnd_has_handitem", "wf_cnd_has_var", "wf_cnd_match_date",
        "wf_cnd_match_snapshot", "wf_cnd_match_time", "wf_cnd_neg_has_var",
        "wf_cnd_not_furni_on", "wf_cnd_not_has_handitem", "wf_cnd_not_hv_avtrs",
        "wf_cnd_not_in_group", "wf_cnd_not_in_team", "wf_cnd_not_match_snap",
        "wf_cnd_not_stuff_is", "wf_cnd_not_trggrer_on", "wf_cnd_not_triggerer_match",
        "wf_cnd_not_user_count", "wf_cnd_not_user_performs_action", "wf_cnd_stuff_is",
        "wf_cnd_team_has_rank", "wf_cnd_team_has_score", "wf_cnd_time_less_than",
        "wf_cnd_time_more_than", "wf_cnd_trggrer_on_frn", "wf_cnd_triggerer_match",
        "wf_cnd_user_count_in", "wf_cnd_user_performs_action", "wf_cnd_valid_moves",
        "wf_cnd_var_age_match", "wf_cnd_var_val_match", "wf_slc_furni_altitude",
        "wf_slc_furni_area", "wf_slc_furni_bytype", "wf_slc_furni_neighborhood",
        "wf_slc_furni_onfurni", "wf_slc_furni_picks", "wf_slc_furni_signal",
        "wf_slc_furni_with_var", "wf_slc_remote", "wf_slc_users_area",
        "wf_slc_users_byaction", "wf_slc_users_byname", "wf_slc_users_bytype",
        "wf_slc_users_group", "wf_slc_users_handitem", "wf_slc_users_neighborhood",
        "wf_slc_users_onfurni", "wf_slc_users_signal", "wf_slc_users_team",
        "wf_slc_users_with_var", "wf_trg_at_given_time", "wf_trg_bot_reached_avtr",
        "wf_trg_bot_reached_stf", "wf_trg_click_furni", "wf_trg_click_tile",
        "wf_trg_click_user", "wf_trg_clock_counter", "wf_trg_collision",
        "wf_trg_enter_room", "wf_trg_game_ends", "wf_trg_game_starts",
        "wf_trg_leave_room", "wf_trg_period_long", "wf_trg_period_short",
        "wf_trg_periodically", "wf_trg_receive_message", "wf_trg_recv_signal",
        "wf_trg_room_initializes", "wf_trg_says_something", "wf_trg_score_achieved",
        "wf_trg_state_changed", "wf_trg_stuff_state", "wf_trg_transaction_complete",
        "wf_trg_transaction_fail", "wf_trg_user_performs_action", "wf_trg_var_changed",
        "wf_trg_walks_off_furni", "wf_trg_walks_on_furni", "wf_var_context",
        "wf_var_furni", "wf_var_room", "wf_var_user",
        "wf_xtra_anim_time", "wf_xtra_exec_in_order", "wf_xtra_execution_limit",
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users",
        "wf_xtra_filter_users_by_var", "wf_xtra_mov_carry_users", "wf_xtra_mov_curve",
        "wf_xtra_mov_no_animation", "wf_xtra_mov_physics", "wf_xtra_random",
        "wf_xtra_rotate_to_dir", "wf_xtra_unseen", "wf_xtra_var_import",
        "wf_xtra_var_lvlup_system", "wf_xtra_var_share", "wf_xtra_var_text_connector",
        "wf_xtra_var_time_util"
    ];

    // Not proven: the card has no Builders Club offer, or the probe could not give it a valid save with picks (chests, coin chests, room linker,
    // dataframes, custom contract, Or Evaluation, the text outputs and inputs, a leaderboard, a variable box, a card that takes only a selector as its
    // furni source), or it has no pick source on Habbo (Link Speaker). Their reopen order stays as saved.
    private static readonly string[] UnprovenCards =
    [
        "wf_act_give_currency", "wf_act_give_effect", "wf_act_give_furni",
        "wf_act_give_reward", "wf_act_init_transaction", "wf_act_move_furni_to",
        "wf_act_mute_triggerer", "wf_act_override_height", "wf_act_progress_ach",
        "wf_act_progress_reward_track", "wf_act_remove_furni", "wf_act_reset_reward_track",
        "wf_act_send_dataframe", "wf_act_teleport_to_room", "wf_cnd_chest_has_items",
        "wf_cnd_not_wearing_b", "wf_cnd_not_wearing_fx", "wf_cnd_slc_quantity",
        "wf_cnd_user_level", "wf_cnd_wearing_badge", "wf_cnd_wearing_effect",
        "wf_trg_at_time_long", "wf_trg_receive_dataframe", "wf_var_dailytask",
        "wf_var_echo", "wf_var_pointer", "wf_var_quest",
        "wf_var_quest_chain", "wf_var_reference", "wf_xtra_achievement_enabler",
        "wf_xtra_custom_contract", "wf_xtra_global_placeholder", "wf_xtra_link_speaker",
        "wf_xtra_or_eval", "wf_xtra_scan_chest_furni_by_type", "wf_xtra_text_input_variable",
        "wf_xtra_text_output_furni_name", "wf_xtra_text_output_username", "wf_xtra_text_output_variable",
        "wf_xtra_var_fx_boss", "wf_xtra_var_fx_health", "wf_xtra_var_fx_level",
        "wf_xtra_var_fx_number", "wf_xtra_var_fx_progress", "wf_xtra_var_fx_status",
        "wf_xtra_var_leaderboard", "wf_xtra_var_web_api"
    ];

    public static TheoryData<string> ProvenCardNames => new(ProvenCards);

    [Theory]
    [MemberData(nameof(ProvenCardNames))]
    public void TheCardReopensItsPicksInHashSetOrder(string card)
    {
        var frame = Frame(card, HabboSent, []);

        Assert.Equal(HabboReopened, Primary(frame));
    }

    [Fact]
    public void FurniToFurniReopensItsTargetPicksInHashSetOrderToo()
    {
        int[] targets = [2147418113, 2147418130, 2147418112, 2147418114, 2147418128];
        int[] reopened = [2147418130, 2147418114, 2147418113, 2147418112, 2147418128];

        var frame = Frame("wf_act_furni_to_furni", HabboSent, targets);

        Assert.Equal(HabboReopened, Primary(frame));
        Assert.Equal(reopened, frame.Skip(2 + HabboSent.Length + 1).Take(targets.Length));
    }

    public static TheoryData<string> UnprovenCardNames => new(UnprovenCards);

    [Theory]
    [MemberData(nameof(UnprovenCardNames))]
    public void AnUnprovenCardKeepsTheSavedOrder(string card)
    {
        Assert.Equal(HabboSent, Primary(Frame(card, HabboSent, [])));
    }

    // A card is either proven above or listed as unproven, so a new card cannot join unnoticed.
    [Fact]
    public void EveryCardIsProvenOrListedAsUnproven()
    {
        var known = ProvenCards.Concat(UnprovenCards).ToHashSet();
        var unclassified = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name))
            .Where(name => !known.Contains(name)).Order().ToArray();

        Assert.True(unclassified.Length == 0, string.Join(", ", unclassified));
    }

    // The flag is set only on proven cards: a card with no proof keeps the saved order.
    [Fact]
    public void OnlyProvenCardsGetHashSetOrder()
    {
        var flagged = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name) && WiredNativeEditorProjection.Metadata(name).HashSetPickOrder)
            .Where(name => !ProvenCards.Contains(name)).Order().ToArray();

        Assert.True(flagged.Length == 0, string.Join(", ", flagged));
    }

    // The second pick list of every proven card, saved and reopened on Habbo, a card with a picker or without. Chest Has Item Type refuses a
    // second list without chests, so its second list is not proven.
    public static TheoryData<string> SecondListCardNames => new(ProvenCards.Where(card => card != "wf_cnd_chest_has_item_type"));

    [Fact]
    public void EveryProvenCardExceptChestHasItemTypeGetsHashSetOrderForTheSecondList()
    {
        var flagged = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name) && WiredNativeEditorProjection.Metadata(name).HashSetSecondaryPickOrder)
            .Order().ToArray();

        string[] secondListOnly = ["wf_act_give_currency", "wf_act_give_furni", "wf_act_teleport_to_room", "wf_cnd_chest_has_items"];

        Assert.Equal(ProvenCards.Where(card => card != "wf_cnd_chest_has_item_type").Concat(secondListOnly).Order(), flagged);
    }

    // Observed on AIR WIN63-202610201032-107964540: Give Currency, Give Furni, Teleport To Room and Chest Has Items refuse any save with picks in
    // the first list without chests or a room linker, but keep a second list saved alone and reopen it in HashSet order (three runs).
    [Theory]
    [InlineData("wf_act_give_currency")]
    [InlineData("wf_act_give_furni")]
    [InlineData("wf_act_teleport_to_room")]
    [InlineData("wf_cnd_chest_has_items")]
    public void ASecondListSavedAloneReopensInHashSetOrderOnACardWhoseFirstListIsUnproven(string card)
    {
        int[] targets = [2147418113, 2147418130, 2147418112, 2147418114, 2147418128];
        int[] reopened = [2147418130, 2147418114, 2147418113, 2147418112, 2147418128];

        var frame = Frame(card, [], targets);

        Assert.Equal(0, frame[1]);
        Assert.Equal(reopened, frame.Skip(3).Take(targets.Length));
    }

    [Theory]
    [MemberData(nameof(SecondListCardNames))]
    public void TheSecondPickListReopensInHashSetOrderToo(string card)
    {
        int[] targets = [2147418113, 2147418130, 2147418112, 2147418114, 2147418128];
        int[] reopened = [2147418130, 2147418114, 2147418113, 2147418112, 2147418128];

        var frame = Frame(card, HabboSent, targets);

        Assert.Equal(HabboReopened, Primary(frame));
        Assert.Equal(reopened, frame.Skip(2 + HabboSent.Length + 1).Take(targets.Length));
    }

    [Fact]
    public void TheSecondListOfChestHasItemTypeKeepsTheSavedOrder()
    {
        int[] targets = [2147418113, 2147418130, 2147418112, 2147418114, 2147418128];

        var frame = Frame("wf_cnd_chest_has_item_type", HabboSent, targets);

        Assert.Equal(targets, frame.Skip(2 + HabboSent.Length + 1).Take(targets.Length));
    }

    // Habbo, 8 ids and then 9 ids in one bucket (ids 16 apart, 16 buckets): the ninth id doubles the table, which splits the bucket. The
    // 18 ids row is from the real JDK 21, not from Habbo.
    [Theory]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132 },
        new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132 })]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132, -2147418148 },
        new[] { 2147418131, 2147418163, 2147418195, -2147418132, 2147418147, 2147418179, 2147418211, -2147418116, -2147418148 })]
    [InlineData(new[] { 2147418131, 2147418147, 2147418163, 2147418179, 2147418195, 2147418211, -2147418116, -2147418132, -2147418148, 2147418129 },
        new[] { 2147418131, 2147418163, 2147418195, -2147418132, 2147418129, 2147418147, 2147418179, 2147418211, -2147418116, -2147418148 })]
    // Real JDK 21 java.util.HashSet, 18 ids 3, 19, ..., 275: the table doubles twice.
    [InlineData(new[] { 3, 19, 35, 51, 67, 83, 99, 115, 131, 147, 163, 179, 195, 211, 227, 243, 259, 275 },
        new[] { 3, 67, 131, 195, 259, 19, 83, 147, 211, 275, 35, 99, 163, 227, 51, 115, 179, 243 })]
    public void NineIdsInOneBucketResizeTheTableLikeJava(int[] sent, int[] reopened)
    {
        Assert.Equal(reopened, Primary(Frame("wf_act_toggle_state", sent, [])));
    }

    // Observed on AIR WIN63-202610201032-107964540 (7 saves that repeat ids, the sent count 12 or 20 against 11 or 10 different ids): the first
    // set of the two steps is sized from the number of ids sent, repeats counted, so the order differs from one sized from the different ids.
    // The probe, in the order of the sent ids, then the order Habbo reopened.
    [Theory]
    [InlineData(new[] { 2147418142, 2147418148, 2147418172, 2147418168, 2147418140, 2147418162, 2147418117, 2147418118, 2147418132, 2147418160, 2147418142, 2147418166 },
        new[] { 2147418142, 2147418172, 2147418140, 2147418168, 2147418166, 2147418118, 2147418117, 2147418132, 2147418148, 2147418162, 2147418160 })] // Habbo, 12 ids sent, 11 different
    [InlineData(new[] { 2147418147, 2147418146, 2147418157, 2147418134, 2147418120, 2147418146, 2147418162, 2147418155, 2147418145, 2147418154, 2147418131, 2147418116 },
        new[] { 2147418157, 2147418155, 2147418154, 2147418120, 2147418134, 2147418116, 2147418131, 2147418147, 2147418162, 2147418146, 2147418145 })] // Habbo, 12 ids sent, 11 different
    [InlineData(new[] { 2147418118, 2147418158, 2147418114, 2147418116, 2147418166, 2147418147, 2147418114, 2147418152, 2147418116, 2147418147, 2147418129, 2147418122, 2147418114, 2147418122, 2147418162, 2147418129, 2147418129, 2147418114, 2147418162, 2147418118 },
        new[] { 2147418158, 2147418122, 2147418152, 2147418166, 2147418118, 2147418116, 2147418147, 2147418162, 2147418114, 2147418129 })] // Habbo, 20 ids sent, 10 different
    [InlineData(new[] { 2147418116, 2147418152, 2147418168, 2147418126, 2147418145, 2147418122, 2147418168, 2147418141, 2147418119, 2147418168, 2147418150, 2147418141, 2147418141, 2147418145, 2147418119, 2147418163, 2147418141, 2147418145, 2147418150, 2147418152 },
        new[] { 2147418126, 2147418141, 2147418122, 2147418168, 2147418152, 2147418119, 2147418150, 2147418116, 2147418163, 2147418145 })] // Habbo, 20 ids sent, 10 different
    [InlineData(new[] { 2147418126, 2147418132, 2147418131, 2147418159, 2147418132, 2147418115, 2147418159, 2147418126, 2147418131, 2147418126, 2147418130, 2147418142, 2147418131, 2147418127, 2147418130, 2147418117, 2147418121, 2147418159, 2147418127, 2147418127 },
        new[] { 2147418159, 2147418127, 2147418142, 2147418126, 2147418121, 2147418117, 2147418132, 2147418131, 2147418115, 2147418130 })] // Habbo, 20 ids sent, 10 different
    [InlineData(new[] { 2147418150, 2147418136, 2147418162, 2147418138, 2147418131, 2147418129, 2147418124, 2147418151, 2147418144, 2147418128, 2147418136, 2147418155 },
        new[] { 2147418124, 2147418155, 2147418138, 2147418136, 2147418151, 2147418150, 2147418131, 2147418162, 2147418129, 2147418128, 2147418144 })] // Habbo, 12 ids sent, 11 different
    [InlineData(new[] { 2147418167, 2147418149, 2147418149, 2147418114, 2147418114, 2147418112, 2147418128, 2147418150, 2147418114, 2147418112, 2147418165, 2147418114, 2147418165, 2147418150, 2147418131, 2147418167, 2147418150, 2147418146, 2147418149, 2147418135 },
        new[] { 2147418167, 2147418135, 2147418150, 2147418165, 2147418149, 2147418131, 2147418114, 2147418146, 2147418128, 2147418112 })] // Habbo, 20 ids sent, 10 different
    public void ASaveThatRepeatsIdsReopensInTheOrderOfTheSentCount(int[] sent, int[] reopened)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [.. sent.Distinct().Select(Item)],
            PrimarySent = sent.Length
        };
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        Assert.Equal(reopened, packet.Writes.Skip(2).Take(reopened.Length).Cast<int>());
    }

    // Observed on AIR WIN63-202610201032-107964540 (four saves of the second list that repeat ids, the sent count 12 or 20 against 11 or 10
    // different ids): the second list follows the same rule as the first.
    [Theory]
    [InlineData(new[] { 2147418116, 2147418152, 2147418168, 2147418126, 2147418145, 2147418122, 2147418168, 2147418141, 2147418119, 2147418168, 2147418150, 2147418141, 2147418141, 2147418145, 2147418119, 2147418163, 2147418141, 2147418145, 2147418150, 2147418152 },
        new[] { 2147418126, 2147418141, 2147418122, 2147418168, 2147418152, 2147418119, 2147418150, 2147418116, 2147418163, 2147418145 })] // Habbo, 20 ids sent, 10 different
    [InlineData(new[] { 2147418126, 2147418132, 2147418131, 2147418159, 2147418132, 2147418115, 2147418159, 2147418126, 2147418131, 2147418126, 2147418130, 2147418142, 2147418131, 2147418127, 2147418130, 2147418117, 2147418121, 2147418159, 2147418127, 2147418127 },
        new[] { 2147418159, 2147418127, 2147418142, 2147418126, 2147418121, 2147418117, 2147418132, 2147418131, 2147418115, 2147418130 })] // Habbo, 20 ids sent, 10 different
    [InlineData(new[] { 2147418150, 2147418136, 2147418162, 2147418138, 2147418131, 2147418129, 2147418124, 2147418151, 2147418144, 2147418128, 2147418136, 2147418155 },
        new[] { 2147418124, 2147418155, 2147418138, 2147418136, 2147418151, 2147418150, 2147418131, 2147418162, 2147418129, 2147418128, 2147418144 })] // Habbo, 12 ids sent, 11 different
    [InlineData(new[] { 2147418167, 2147418149, 2147418149, 2147418114, 2147418114, 2147418112, 2147418128, 2147418150, 2147418114, 2147418112, 2147418165, 2147418114, 2147418165, 2147418150, 2147418131, 2147418167, 2147418150, 2147418146, 2147418149, 2147418135 },
        new[] { 2147418167, 2147418135, 2147418150, 2147418165, 2147418149, 2147418131, 2147418114, 2147418146, 2147418128, 2147418112 })] // Habbo, 20 ids sent, 10 different
    public void ASecondListThatRepeatsIdsReopensInTheOrderOfTheSentCount(int[] sent, int[] reopened)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            SecondaryItems = [.. sent.Distinct().Select(Item)],
            SecondarySent = sent.Length
        };
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        Assert.Equal(reopened, packet.Writes.Skip(3).Take(reopened.Length).Cast<int>());
    }

    // A stored count that cannot come from a save (more than the limit, or no more than the picks kept) is out of bounds.
    [Theory]
    [InlineData(0, true)]
    [InlineData(12, true)]
    [InlineData(20, true)]
    [InlineData(21, false)]
    [InlineData(5, false)]
    [InlineData(int.MaxValue, false)]
    public void ASentCountMustBeZeroOrFitASave(int sent, bool fits)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { PrimaryItems = [.. Enumerable.Range(1, 5).Select(Item)], PrimarySent = sent };

        Assert.Equal(fits, WiredNativeEditorProjection.WithinBounds(native));
    }

    // The stored row keeps the sent count, so a reload (an unload of the room on Habbo gave the same order) reopens the same order.
    [Fact]
    public void TheSentCountIsPartOfTheStoredRow()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        int[] sent = [2147418142, 2147418148, 2147418172, 2147418168, 2147418140, 2147418162, 2147418117, 2147418118, 2147418132, 2147418160, 2147418142, 2147418166];
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { PrimaryItems = [.. sent.Distinct().Select(Item)], PrimarySent = sent.Length };

        var runtime = WiredConfigurationStore.DecodeRow(9, "wf_act_toggle_state", System.Text.Json.JsonSerializer.Serialize(native));

        Assert.Equal(sent.Length, runtime!.Origin!.Native.PrimarySent);
    }

    private static int[] Primary(int[] frame) => [.. frame.Skip(2).Take(frame[1])];

    private static int[] Frame(string card, int[] primary, int[] secondary)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [.. primary.Select(Item)],
            SecondaryItems = [.. secondary.Select(Item)]
        };
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        return [.. packet.Writes.Select(write => write is int value ? value : 0)];
    }

    private static WiredNativeItemReference Item(int id) => new((uint)Math.Abs(id), id < 0);
}
