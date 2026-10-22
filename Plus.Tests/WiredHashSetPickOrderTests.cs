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
        "wf_act_adjust_clock",
        "wf_act_bot_move",
        "wf_act_bot_teleport",
        "wf_act_call_stacks",
        "wf_act_cancel_transaction",
        "wf_act_change_var_val",
        "wf_act_chase",
        "wf_act_control_clock",
        "wf_act_flee",
        "wf_act_furni_to_furni",
        "wf_act_furni_to_user",
        "wf_act_give_var",
        "wf_act_match_to_sshot",
        "wf_act_move_furni_as_group",
        "wf_act_move_rotate",
        "wf_act_move_to_dir",
        "wf_act_neg_call_stacks",
        "wf_act_neg_send_signal",
        "wf_act_place_furni",
        "wf_act_rel_mov",
        "wf_act_remove_var",
        "wf_act_send_message",
        "wf_act_send_signal",
        "wf_act_set_altitude",
        "wf_act_set_highlight",
        "wf_act_set_visibility",
        "wf_act_teleport_to",
        "wf_act_toggle_state",
        "wf_act_toggle_to_rnd",
        "wf_act_user_to_furni",
        "wf_cnd_chest_has_item_type",
        "wf_cnd_counter_time_matches",
        "wf_cnd_furnis_hv_avtrs",
        "wf_cnd_has_altitude",
        "wf_cnd_has_furni_on",
        "wf_cnd_has_var",
        "wf_cnd_match_snapshot",
        "wf_cnd_neg_has_var",
        "wf_cnd_not_furni_on",
        "wf_cnd_not_hv_avtrs",
        "wf_cnd_not_match_snap",
        "wf_cnd_not_stuff_is",
        "wf_cnd_not_trggrer_on",
        "wf_cnd_stuff_is",
        "wf_cnd_trggrer_on_frn",
        "wf_cnd_var_age_match",
        "wf_cnd_var_val_match",
        "wf_slc_furni_bytype",
        "wf_slc_furni_neighborhood",
        "wf_slc_furni_onfurni",
        "wf_slc_furni_picks",
        "wf_slc_furni_with_var",
        "wf_slc_remote",
        "wf_slc_users_neighborhood",
        "wf_slc_users_onfurni",
        "wf_slc_users_with_var",
        "wf_trg_bot_reached_stf",
        "wf_trg_click_furni",
        "wf_trg_click_tile",
        "wf_trg_clock_counter",
        "wf_trg_receive_message",
        "wf_trg_recv_signal",
        "wf_trg_state_changed",
        "wf_trg_stuff_state",
        "wf_trg_transaction_complete",
        "wf_trg_transaction_fail",
        "wf_trg_walks_off_furni",
        "wf_trg_walks_on_furni",
        "wf_xtra_filter_furni",
        "wf_xtra_filter_furni_by_var",
        "wf_xtra_filter_users",
        "wf_xtra_filter_users_by_var",
        "wf_xtra_mov_curve",
        "wf_xtra_mov_physics",
        "wf_xtra_rotate_to_dir"
    ];

    // Not proven: the card has no Builders Club offer (Move Furni To, Override Height, the two progress cards), or the probe could not give it
    // a valid save with picks (chests, coin chests, room linker, dataframes, custom contract, Or Evaluation, the two text outputs), or it has no pick source on Habbo (Link
    // Speaker). Their reopen order stays as saved.
    private static readonly string[] UnprovenCards =
    [
        "wf_act_move_furni_to", "wf_act_init_transaction", "wf_cnd_chest_has_items", "wf_act_give_currency", "wf_act_give_furni",
        "wf_act_teleport_to_room", "wf_act_send_dataframe", "wf_trg_receive_dataframe", "wf_act_override_height", "wf_act_progress_ach",
        "wf_act_progress_reward_track", "wf_xtra_custom_contract", "wf_xtra_or_eval", "wf_xtra_scan_chest_furni_by_type", "wf_xtra_link_speaker",
        "wf_xtra_text_output_furni_name", "wf_xtra_text_output_variable"
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

    [Theory]
    [InlineData("wf_act_move_furni_to")]
    [InlineData("wf_act_init_transaction")]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_act_give_currency")]
    [InlineData("wf_act_give_furni")]
    [InlineData("wf_act_teleport_to_room")]
    [InlineData("wf_act_send_dataframe")]
    [InlineData("wf_trg_receive_dataframe")]
    [InlineData("wf_act_override_height")]
    [InlineData("wf_act_progress_ach")]
    [InlineData("wf_act_progress_reward_track")]
    [InlineData("wf_xtra_custom_contract")]
    [InlineData("wf_xtra_or_eval")]
    [InlineData("wf_xtra_scan_chest_furni_by_type")]
    [InlineData("wf_xtra_link_speaker")]
    [InlineData("wf_xtra_text_output_furni_name")]
    [InlineData("wf_xtra_text_output_variable")]
    public void AnUnprovenCardKeepsTheSavedOrder(string card)
    {
        Assert.Equal(HabboSent, Primary(Frame(card, HabboSent, [])));
    }

    // A card with a picker is either proven above or listed as unproven, so a new card cannot join unnoticed.
    [Fact]
    public void EveryCardWithAPickerIsProvenOrListedAsUnproven()
    {
        var known = ProvenCards.Concat(UnprovenCards).ToHashSet();
        var unclassified = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name) && WiredNativeEditorProjection.Metadata(name).FurniAllowed.Any(group => group.Contains(100)))
            .Where(name => !known.Contains(name)).Order().ToArray();

        Assert.True(unclassified.Length == 0, string.Join(", ", unclassified));
    }

    // The flag is set only on proven cards: a card with no picker or no proof keeps the saved order.
    [Fact]
    public void OnlyProvenCardsGetHashSetOrder()
    {
        var flagged = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name) && WiredNativeEditorProjection.Metadata(name).HashSetPickOrder)
            .Where(name => !ProvenCards.Contains(name)).Order().ToArray();

        Assert.True(flagged.Length == 0, string.Join(", ", flagged));
    }

    [Fact]
    public void OnlyTheThirteenProvenCardsGetHashSetOrderForTheSecondList()
    {
        string[] expected =
        [
            "wf_act_change_var_val", "wf_act_furni_to_furni", "wf_act_give_var", "wf_act_move_furni_as_group", "wf_act_neg_send_signal",
            "wf_act_place_furni", "wf_act_send_signal", "wf_cnd_not_stuff_is", "wf_cnd_stuff_is", "wf_cnd_var_age_match", "wf_cnd_var_val_match",
            "wf_xtra_mov_physics", "wf_xtra_rotate_to_dir"
        ];
        var flagged = WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName)
            .Where(name => WiredNativeEditorProjection.Supports(name) && WiredNativeEditorProjection.Metadata(name).HashSetSecondaryPickOrder)
            .Order().ToArray();

        Assert.Equal(expected, flagged);
    }

    // The second pick list of every proven card that has one, saved and reopened on Habbo. Chest Has Item Type refuses a second list
    // without chests, so its second list is not proven.
    [Theory]
    [InlineData("wf_act_change_var_val")]
    [InlineData("wf_act_furni_to_furni")]
    [InlineData("wf_act_give_var")]
    [InlineData("wf_act_move_furni_as_group")]
    [InlineData("wf_act_neg_send_signal")]
    [InlineData("wf_act_place_furni")]
    [InlineData("wf_act_send_signal")]
    [InlineData("wf_cnd_not_stuff_is")]
    [InlineData("wf_cnd_stuff_is")]
    [InlineData("wf_cnd_var_age_match")]
    [InlineData("wf_cnd_var_val_match")]
    [InlineData("wf_xtra_mov_physics")]
    [InlineData("wf_xtra_rotate_to_dir")]
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
