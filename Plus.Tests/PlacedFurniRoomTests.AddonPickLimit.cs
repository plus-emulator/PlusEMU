using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The limit of 20 ids in one pick list holds on the add-ons and on the variable boxes too. A save of 21 gets no answer
// and leaves the picks as they were. A variable box with no name answers its name key at 20 picks and nothing at 21. Cards that refuse picks of a wrong kind (a Click Tile trigger, Control Clock, Send Signal,
// Toggle State with a temporary copy) answer their key at 19 and 20 picks and nothing at 21.
public partial class PlacedFurniRoomTests
{
    public static TheoryData<string> CardsWithAPickLimit() => new(
    [
        "wf_var_furni", "wf_var_user", "wf_var_room", "wf_var_context",
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users", "wf_xtra_filter_users_by_var", "wf_xtra_mov_curve",
        "wf_xtra_mov_physics", "wf_xtra_rotate_to_dir", "wf_xtra_random", "wf_xtra_unseen", "wf_xtra_exec_in_order", "wf_xtra_execution_limit",
        "wf_xtra_mov_carry_users", "wf_xtra_mov_no_animation", "wf_xtra_anim_time", "wf_xtra_var_time_util", "wf_xtra_var_text_connector",
        "wf_xtra_var_share", "wf_xtra_var_import"
    ]);

    [Theory]
    [MemberData(nameof(CardsWithAPickLimit))]
    public void AnAddOnOrAVariableBoxSavesTwentyPicksAndDropsASaveOfTwentyOne(string card)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();

        foreach (var id in Enumerable.Range(0, 21)) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + (uint)id, "tile"), id % 4, 3 - id / 8 % 2, 0, true,
                false, false));
        }

        PlaceVariableBox(SecondListBox, card);

        Assert.Equal("saved", SaveLists(service, descriptor, [.. Enumerable.Range(0, 20)], []));
        Assert.Equal(20, ReopenLists().First.Length);
        Assert.Equal("dropped", SaveLists(service, descriptor, [.. Enumerable.Range(0, 21)], []));
        Assert.Equal(20, ReopenLists().First.Length);
    }

    [Theory]
    [InlineData("wf_trg_click_tile", "wiredfurni.error.require_click_tiles", false)]
    [InlineData("wf_act_control_clock", "wiredfurni.error.require_counter_furni", false)]
    [InlineData("wf_act_send_signal", "wiredfurni.error.require_antenna_furni", false)]
    [InlineData("wf_act_toggle_state", "wiredfurni.error.temp_furni_not_allowed", true)]
    public void AKeyedRefusalOfThePicksComesAfterTheLimit(string card, string refusal, bool temporaryCopy)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();

        foreach (var id in Enumerable.Range(0, temporaryCopy ? 20 : 21)) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + (uint)id, "tile"), id % 4, 3 - id / 8 % 2, 0, true,
                false, false));
        }

        if (temporaryCopy) {
            // The copy Place Temporary Furni makes: an id of the temporary band.
            var copy = new Plus.HabboHotel.Items.Item
            {
                Id = Plus.HabboHotel.Items.TemporaryItemIds.First,
                OwnerId = 7,
                RoomId = RoomId,
                IsTemporary = true,
                GetX = 0,
                GetY = 0,
                Definition = SecondListFurni(1, "tile").Definition
            };
            _room.GetRoomItemHandler().LoadFurniture([copy]);
            Assert.NotNull(_room.GetRoomItemHandler().GetItem(Plus.HabboHotel.Items.TemporaryItemIds.First));
        }

        PlaceVariableBox(SecondListBox, card);
        int[] nineteen = [.. Enumerable.Range(0, 19)], twenty = [.. Enumerable.Range(0, 20)], twentyOne = [.. Enumerable.Range(0, 21)];

        if (temporaryCopy) {
            var copy = (int)(Plus.HabboHotel.Items.TemporaryItemIds.First - SecondListBase);

            Assert.Equal(refusal, SaveLists(service, descriptor, [.. nineteen, copy], []));
            Assert.Equal("dropped", SaveLists(service, descriptor, [.. twenty, copy], []));
        }
        else {
            Assert.Equal(refusal, SaveLists(service, descriptor, nineteen, []));
            Assert.Equal(refusal, SaveLists(service, descriptor, twenty, []));
            Assert.Equal("dropped", SaveLists(service, descriptor, twentyOne, []));
        }
    }

    [Theory]
    [InlineData("wf_var_furni", "wiredfurni.error.variables.name_length")]
    [InlineData("wf_var_user", "wiredfurni.error.variables.name_length")]
    public void ANameRefusalComesAfterTheLimit(string card, string refusal)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();

        foreach (var id in Enumerable.Range(0, 21)) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni(SecondListBase + (uint)id, "tile"), id % 4, 3 - id / 8 % 2, 0, true,
                false, false));
        }

        PlaceVariableBox(SecondListBox, card);

        Assert.Equal(refusal, SaveLists(service, descriptor, [.. Enumerable.Range(0, 20)], [], ""));
        Assert.Equal("dropped", SaveLists(service, descriptor, [.. Enumerable.Range(0, 21)], [], ""));
    }
}
