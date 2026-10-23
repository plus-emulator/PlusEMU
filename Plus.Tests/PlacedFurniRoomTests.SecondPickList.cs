using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (three runs, 70 cards of every kind: actions, conditions, triggers, selectors and add-ons): every
// card keeps a second pick list next to its first one, whether or not it uses one. Saved with the first list [16, 2, 0, 18, 1] and the second
// [1, 18, 0, 2, 16] (ids 2147418112 plus these), a card reopens [2, 18, 1, 16, 0] and [18, 2, 1, 0, 16]: each list in the iteration order of a
// Java HashSet. The second list alone is kept as well, a second list that names no furni is saved and comes back empty, and an emptied second
// list reopens empty. These tests save through the handler with ids 600 to 618, which fall into the same buckets as the probe's ids (those sit in the
// Builders Club id band that other tests hand out), so the lists reopen in the order of that rule for these ids. The probe's own ids are checked in
// WiredHashSetPickOrderTests. Variable Age and Variable Changed saved a fresh box on Habbo and are refused here, and Chest Has Item Type refuses a
// second list without chests; they are covered there only.
public partial class PlacedFurniRoomTests
{
    private const uint SecondListBox = 700, SecondListBase = 600;
    private static readonly int[] SecondListFirst = [16, 2, 0, 18, 1], SecondListSecond = [1, 18, 0, 2, 16];
    private static readonly int[] SecondListFirstReopened = [16, 0, 1, 2, 18], SecondListSecondReopened = [0, 16, 1, 18, 2];

    // The cards of the probe that need only plain furni as picks (colour tiles).
    public static TheoryData<string> PlainPickCards() => new(
    [
        "wf_act_bot_move", "wf_act_bot_teleport", "wf_act_call_stacks", "wf_act_change_var_val", "wf_act_chase", "wf_act_flee", "wf_act_furni_to_furni",
        "wf_act_furni_to_user", "wf_act_give_var", "wf_act_match_to_sshot", "wf_act_move_furni_as_group", "wf_act_move_rotate", "wf_act_move_to_dir",
        "wf_act_neg_call_stacks", "wf_act_place_furni", "wf_act_rel_mov", "wf_act_remove_var", "wf_act_set_altitude", "wf_act_set_highlight",
        "wf_act_set_visibility", "wf_act_teleport_to", "wf_act_toggle_state", "wf_act_toggle_to_rnd", "wf_act_user_to_furni",
        "wf_cnd_furnis_hv_avtrs", "wf_cnd_has_altitude", "wf_cnd_has_furni_on", "wf_cnd_has_var", "wf_cnd_match_snapshot", "wf_cnd_neg_has_var",
        "wf_cnd_not_furni_on", "wf_cnd_not_hv_avtrs", "wf_cnd_not_match_snap", "wf_cnd_not_stuff_is", "wf_cnd_not_trggrer_on", "wf_cnd_stuff_is",
        "wf_cnd_trggrer_on_frn", "wf_cnd_var_val_match", "wf_slc_furni_bytype", "wf_slc_furni_neighborhood",
        "wf_slc_furni_onfurni", "wf_slc_furni_picks", "wf_slc_furni_with_var", "wf_slc_remote", "wf_slc_users_neighborhood", "wf_slc_users_onfurni",
        "wf_slc_users_with_var", "wf_trg_bot_reached_stf", "wf_trg_click_furni", "wf_trg_state_changed", "wf_trg_stuff_state",
        "wf_trg_walks_off_furni", "wf_trg_walks_on_furni", "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users",
        "wf_xtra_filter_users_by_var", "wf_xtra_mov_curve", "wf_xtra_mov_physics", "wf_xtra_rotate_to_dir"
    ]);

    // The cards of the probe with no picker of their own (a user condition, a clock trigger, a text add-on, ...): Habbo kept both lists on them too.
    public static TheoryData<string> NoPickerCards() => new(
    [
        "wf_act_bot_clothes", "wf_act_bot_follow_avatar", "wf_act_bot_give_handitem",
        "wf_act_bot_talk", "wf_act_bot_talk_to_avatar", "wf_act_click_conf",
        "wf_act_freeze", "wf_act_give_score", "wf_act_give_score_tm",
        "wf_act_join_team", "wf_act_kick_user", "wf_act_leave_team",
        "wf_act_log", "wf_act_move_rotate_user", "wf_act_neg_log",
        "wf_act_reset_timers", "wf_act_show_message", "wf_act_unfreeze",
        "wf_cnd_actor_dir", "wf_cnd_actor_in_group", "wf_cnd_actor_in_team",
        "wf_cnd_date_rng_active", "wf_cnd_has_handitem", "wf_cnd_match_date",
        "wf_cnd_match_time", "wf_cnd_not_has_handitem", "wf_cnd_not_in_group",
        "wf_cnd_not_in_team", "wf_cnd_not_triggerer_match", "wf_cnd_not_user_count",
        "wf_cnd_not_user_performs_action", "wf_cnd_team_has_rank", "wf_cnd_team_has_score",
        "wf_cnd_time_less_than", "wf_cnd_time_more_than", "wf_cnd_triggerer_match",
        "wf_cnd_user_count_in", "wf_cnd_user_performs_action", "wf_cnd_valid_moves",
        "wf_trg_room_initializes", "wf_trg_at_given_time", "wf_trg_bot_reached_avtr",
        "wf_trg_click_user", "wf_trg_collision", "wf_trg_enter_room",
        "wf_trg_game_ends", "wf_trg_game_starts", "wf_trg_leave_room",
        "wf_trg_period_long", "wf_trg_period_short", "wf_trg_periodically",
        "wf_trg_says_something", "wf_trg_score_achieved", "wf_trg_user_performs_action",
        "wf_slc_furni_altitude", "wf_slc_furni_area", "wf_slc_furni_signal",
        "wf_slc_users_area", "wf_slc_users_byaction", "wf_slc_users_byname",
        "wf_slc_users_bytype", "wf_slc_users_group", "wf_slc_users_handitem",
        "wf_slc_users_signal", "wf_slc_users_team", "wf_xtra_random",
        "wf_xtra_unseen", "wf_act_send_message", "wf_trg_receive_message",
        "wf_xtra_var_import", "wf_xtra_var_share", "wf_xtra_var_lvlup_system",
        "wf_xtra_exec_in_order", "wf_xtra_execution_limit", "wf_xtra_mov_carry_users",
        "wf_xtra_mov_no_animation", "wf_xtra_anim_time", "wf_xtra_var_time_util",
        "wf_xtra_var_text_connector"
    ]);

    // The cards of the probe that refuse a pick of any other kind of furni, with the kind they were saved with.
    public static TheoryData<string, string> SpecialPickCards() => new()
    {
        { "wf_act_adjust_clock", "counter" }, { "wf_act_control_clock", "counter" }, { "wf_cnd_counter_time_matches", "counter" },
        { "wf_trg_clock_counter", "counter" }, { "wf_act_send_signal", "antenna" }, { "wf_act_neg_send_signal", "antenna" },
        { "wf_trg_recv_signal", "antenna" }, { "wf_trg_click_tile", "click tile" }, { "wf_act_cancel_transaction", "contract" },
        { "wf_trg_transaction_complete", "contract" }, { "wf_trg_transaction_fail", "contract" }
    };

    // The variable boxes of the probe: they keep both lists too.
    public static TheoryData<string> VariableBoxCards() => new(["wf_var_furni", "wf_var_user", "wf_var_room", "wf_var_context"]);

    [Theory]
    [MemberData(nameof(PlainPickCards))]
    [MemberData(nameof(NoPickerCards))]
    [MemberData(nameof(VariableBoxCards))]
    public void ACardKeepsItsSecondPickListInHashSetOrder(string card) => CheckSecondPickList(card, "tile");

    [Theory]
    [MemberData(nameof(SpecialPickCards))]
    public void ACardWithPicksOfOneKindKeepsItsSecondPickListInHashSetOrder(string card, string kind) => CheckSecondPickList(card, kind);

    // A second-list pick of the wrong kind is still refused (only one that names no furni is dropped): a plain furni in the second list of a Click Tile
    // trigger or of a contract card, next to a first list of the right kind.
    [Theory]
    [InlineData("wf_trg_click_tile", "click tile", "wiredfurni.error.require_click_tiles")]
    [InlineData("wf_act_cancel_transaction", "contract", "wiredfurni.error.require_contracts")]
    [InlineData("wf_trg_transaction_complete", "contract", "wiredfurni.error.require_contracts_or_trade_wired")]
    public void ALiveSecondPickOfTheWrongKindIsStillRefused(string card, string kind, string refusal)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();

        foreach (var id in new[] { 0, 1, 2, 16, 18 }) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + (uint)id, kind), id % 4, 3 - id / 8 % 2, 0, true, false, false));
        }

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + 50, "tile"), 3, 1, 0, true, false, false));
        PlaceVariableBox(SecondListBox, card);

        Assert.Equal(refusal, SaveLists(service, descriptor, SecondListFirst, [50]));
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, [1]));
    }

    private void CheckSecondPickList(string card, string kind)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();

        foreach (var id in new[] { 0, 1, 2, 16, 18 }) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + (uint)id, kind), id % 4, 3 - id / 8 % 2, 0, true,
                false, false));
        }

        PlaceVariableBox(SecondListBox, card);

        // First list only: no second list.
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, []));
        AssertLists(SecondListFirstReopened, []);

        // Both lists.
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, SecondListSecond));
        AssertLists(SecondListFirstReopened, SecondListSecondReopened);

        // A second list with an empty first list.
        Assert.Equal("saved", SaveLists(service, descriptor, [], SecondListSecond));
        AssertLists([], SecondListSecondReopened);

        // A second list that names no furni is saved and comes back empty.
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, [399]));
        AssertLists(SecondListFirstReopened, []);

        // Emptied again.
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, SecondListSecond));
        Assert.Equal("saved", SaveLists(service, descriptor, SecondListFirst, []));
        AssertLists(SecondListFirstReopened, []);
    }

    // A Builders Club colour tile, which Place Temporary Furni takes as a template, or a furni of one of the kinds a card insists on.
    private static Item SecondListFurni(uint id, string kind) => new()
    {
        Id = id,
        OwnerId = 7,
        IsBuildersClub = true,
        Definition = new ItemDefinition
        {
            Type = ItemType.Floor,
            SpriteId = 10,
            Height = 0.5,
            Modes = 1,
            Stackable = true,
            InteractionType = kind == "contract" ? InteractionType.WiredContractTrade : InteractionType.None,
            InteractionName = kind switch { "antenna" => "antenna", "click tile" => "room_invisible_click_tile", "counter" => "wf_upcounter1", _ => "" },
            WiredType = WiredBoxType.None,
            ItemName = kind == "counter" ? "wf_upcounter1" : "",
            PublicName = "",
            VendingIds = new List<int>(),
            AdjustableHeights = new List<double>()
        }
    };

    private void AssertLists(int[] first, int[] second)
    {
        var (actualFirst, actualSecond) = ReopenLists();
        Assert.Equal(first, actualFirst);
        Assert.Equal(second, actualSecond);
    }

    // The raw save of the card's own kind, from its default editor record: ints, text, picks, then its footer (delay, quantifier or the two
    // selector flags), furni and user sources, variables and the second picks. The picks are furni 600 plus nn.
    private string SaveLists(WiredConfigurationService service, WiredBoxDescriptor descriptor, int[] first, int[] second, string? name = null)
    {
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        var furni = native.FurniSourceTypes.ToArray();

        if (furni.Length > 0 && !furni.Contains(100)) {
            furni[0] = 100;
        }

        // A variable box needs a name.
        var text = name ?? (descriptor.Category == WiredBoxCategory.Variable ? "r125n" : native.Text);
        List<object> body = [(int)SecondListBox, native.OwnedIntParams.Length, .. native.OwnedIntParams.Cast<object>(), text,
            first.Length, .. first.Select(id => (object)(int)(SecondListBase + id))];

        switch (descriptor.Category) {
            case WiredBoxCategory.Action:
            case WiredBoxCategory.Condition:
                body.Add(0);
                break;
            case WiredBoxCategory.Selector:
                body.AddRange([false, false]);
                break;
        }

        body.AddRange([furni.Length, .. furni.Cast<object>(), native.UserSourceTypes.Length, .. native.UserSourceTypes.Cast<object>(),
            native.VariableIds.Length, .. native.VariableIds.Cast<object>(), second.Length,
            .. second.Select(id => (object)(int)(SecondListBase + id))]);
        IPacketEvent handler = descriptor.Category switch
        {
            WiredBoxCategory.Action => new SaveWiredEffectConfigEvent(service),
            WiredBoxCategory.Condition => new SaveWiredConditionConfigEvent(service),
            WiredBoxCategory.Trigger => new SaveWiredTriggerConfigEvent(service),
            WiredBoxCategory.Selector => new SaveWiredSelectorConfigEvent(service),
            WiredBoxCategory.Variable => new SaveWiredVariableConfigEvent(service),
            _ => new SaveWiredAddonConfigEvent(service)
        };
        _client.Packets.Clear();
        handler.Parse(_client, ClientPacket([.. body])).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    // The raw WIRED_OPEN packet, then the two pick lists of the editor frame that follows the furni limit, as offsets from 600.
    private (int[] First, int[] Second) ReopenLists()
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)SecondListBox)).GetAwaiter().GetResult();
        uint[] headers = [ServerPacketHeader.WiredEffectConfigComposer, ServerPacketHeader.WiredConditionConfigComposer,
            ServerPacketHeader.WiredSelectorConfigComposer, ServerPacketHeader.WiredAddonConfigComposer, ServerPacketHeader.WiredTriggeRconfigComposer,
            ServerPacketHeader.WiredVariableConfigComposer];
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => headers.Contains(packet.Header)).Body };
        frame.ReadInt();
        int[] first = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt() - (int)SecondListBase)];
        int[] second = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt() - (int)SecondListBase)];

        return (first, second);
    }
}
