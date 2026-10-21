using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A row saved before the save rules were relaxed keeps loading in the form it was saved in: only a new save is fitted.
public sealed class WiredSaveLeniencyStoredRowTests
{
    private const string Boxes = "wf_act_chase,wf_act_flee,wf_act_neg_call_stacks,wf_act_toggle_to_rnd,wf_act_reset_timers,wf_act_unfreeze,"
        + "wf_act_move_furni_as_group,wf_act_neg_send_signal,wf_cnd_stuff_is,wf_cnd_not_stuff_is,wf_cnd_trggrer_on_frn,wf_cnd_not_trggrer_on,"
        + "wf_cnd_actor_dir,wf_cnd_actor_in_team,wf_cnd_not_in_team,wf_cnd_counter_time_matches,wf_cnd_furnis_hv_avtrs,wf_cnd_not_furni_on,"
        + "wf_cnd_not_hv_avtrs,wf_cnd_has_altitude,wf_cnd_not_user_count,wf_cnd_slc_quantity,wf_cnd_team_has_rank,wf_cnd_team_has_score,"
        + "wf_cnd_time_less_than,wf_cnd_time_more_than,wf_cnd_has_handitem,wf_cnd_not_has_handitem,wf_cnd_actor_in_group,wf_cnd_not_in_group,"
        + "wf_cnd_chest_has_items,wf_cnd_chest_has_item_type,wf_trg_collision,wf_trg_state_changed,wf_trg_walks_off_furni,wf_trg_click_tile,"
        + "wf_trg_click_user,wf_trg_clock_counter,wf_trg_stuff_state,wf_trg_bot_reached_avtr,wf_trg_bot_reached_stf,wf_slc_users_onfurni,"
        + "wf_slc_furni_signal,wf_slc_furni_altitude,wf_slc_furni_area,wf_slc_furni_bytype,wf_slc_furni_neighborhood,wf_slc_remote,"
        + "wf_slc_users_bytype,wf_slc_users_byaction,wf_slc_users_group,wf_slc_users_handitem,wf_xtra_text_output_furni_name";

    public static TheoryData<string> Touched => [.. Boxes.Split(',')];

    private static WiredConfiguration Decode(string name, WiredNativeEditorConfiguration native)
    {
        var runtime = WiredConfigurationStore.DecodeRow(1, name, JsonSerializer.Serialize(native));

        return Assert.IsType<WiredConfiguration>(runtime);
    }

    private static WiredNativeEditorConfiguration Fresh(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return WiredNativeEditorProjection.DefaultNative(descriptor);
    }

    [Theory]
    [MemberData(nameof(Touched))]
    public void AFreshRowLoadsAsItWasStored(string name)
    {
        var native = Fresh(name);

        Assert.Equal(JsonSerializer.Serialize(native), JsonSerializer.Serialize(Decode(name, native).Origin!.Native));
    }

    [Fact]
    public void AMoveAsGroupRowThatKeptATextStillLoadsWithIt()
    {
        var native = Fresh("wf_act_move_furni_as_group") with { Text = "abc" };

        Assert.Equal("abc", Decode("wf_act_move_furni_as_group", native).Origin!.Native.Text);
    }

    [Fact]
    public void ANeighborhoodRowOfSeventeenIntsStillLoadsWithSeventeen()
    {
        int[] saved = [1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var native = Fresh("wf_slc_furni_neighborhood") with { OwnedIntParams = [.. saved] };

        Assert.Equal(saved, Decode("wf_slc_furni_neighborhood", native).Origin!.Native.OwnedIntParams.ToArray());
    }

    [Fact]
    public void AnAreaRowKeepsItsStoredInts()
    {
        var native = Fresh("wf_slc_furni_area") with { OwnedIntParams = [6, 10, 3, 1] };

        Assert.Equal([6, 10, 3, 1], Decode("wf_slc_furni_area", native).Origin!.Native.OwnedIntParams.ToArray());
    }

    [Fact]
    public void AHandItemRowKeepsItsStoredInt()
    {
        var native = Fresh("wf_cnd_has_handitem") with { OwnedIntParams = [27] };

        Assert.Equal([27], Decode("wf_cnd_has_handitem", native).Origin!.Native.OwnedIntParams.ToArray());
    }
}
