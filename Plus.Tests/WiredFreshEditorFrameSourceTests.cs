using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>Initial editor frames of fresh boxes (allowed lists and defaults).</summary>
public sealed class WiredFreshEditorFrameSourceTests
{
    [Theory]
    [InlineData("wf_xtra_filter_furni", "0,100,201", "100", "0,201", "0")]
    [InlineData("wf_xtra_filter_users", "0,100,201", "100", "0,201", "0")]
    [InlineData("wf_cnd_stuff_is", "0,100,101,200,201|0,100,101,200,201", "0,100", "", "")]
    [InlineData("wf_cnd_not_stuff_is", "0,100,101,200,201|0,100,101,200,201", "0,100", "", "")]
    [InlineData("wf_cnd_team_has_rank", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_team_has_score", "", "", "0,200,201", "0")]
    [InlineData("wf_slc_users_onfurni", "0,100,201", "100", "", "")]
    [InlineData("wf_slc_furni_onfurni", "0,100,201", "100", "", "")]
    [InlineData("wf_slc_furni_bytype", "0,100,201", "100", "", "")]
    [InlineData("wf_slc_remote", "0,100,201", "100", "", "")]
    [InlineData("wf_slc_furni_neighborhood", "0,100,201", "100", "0,201", "0")]
    [InlineData("wf_slc_users_neighborhood", "0,100,201", "100", "0,201", "0")]
    [InlineData("wf_cnd_slc_quantity", "0,200,201", "200", "0,200,201", "200")]
    [InlineData("wf_cnd_triggerer_match", "", "", "0,200,201|0,101,200,201", "0,101")]
    [InlineData("wf_cnd_not_triggerer_match", "", "", "0,200,201|0,101,200,201", "0,101")]
    [InlineData("wf_cnd_match_snapshot", "0,100,200,201", "100", "", "")]
    [InlineData("wf_act_match_to_sshot", "0,100,200,201", "100", "", "")]
    [InlineData("wf_act_move_furni_as_group", "0,100,101,200,201|0,100,101,200,201", "100,0", "0,200,201", "0")]
    [InlineData("wf_act_adjust_clock", "0,100,200,201", "100", "", "")]
    [InlineData("wf_cnd_counter_time_matches", "0,100,200,201", "100", "", "")]
    [InlineData("wf_act_control_clock", "0,100,200,201", "100", "", "")]
    [InlineData("wf_cnd_has_var", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_cnd_neg_has_var", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_cnd_trggrer_on_frn", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_cnd_not_trggrer_on", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_act_freeze", "", "", "0,200,201", "0")]
    [InlineData("wf_act_unfreeze", "", "", "0,200,201", "0")]
    [InlineData("wf_act_kick_user", "", "", "0,200,201", "0")]
    [InlineData("wf_act_join_team", "", "", "0,200,201", "0")]
    [InlineData("wf_act_leave_team", "", "", "0,200,201", "0")]
    [InlineData("wf_act_give_score", "", "", "0,200,201", "0")]
    [InlineData("wf_act_move_rotate_user", "", "", "0,200,201", "0")]
    [InlineData("wf_act_click_conf", "", "", "0,200,201", "0")]
    [InlineData("wf_act_furni_to_user", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_act_user_to_furni", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_act_teleport_to", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_act_teleport_to_room", "0,100,200,201", "100", "0,200,201", "0")]
    [InlineData("wf_xtra_text_output_username", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_actor_in_team", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_not_in_team", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_has_handitem", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_actor_in_group", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_user_performs_action", "", "", "0,200,201", "0")]
    [InlineData("wf_cnd_actor_dir", "", "", "0,200,201", "0")]
    [InlineData("wf_act_bot_talk", "", "", "0,100,200,201", "100")]
    [InlineData("wf_act_send_signal", "0,100,101,200,201|0,100,101,200,201", "100,200", "0,200,201", "200")]
    [InlineData("wf_act_bot_give_handitem", "", "", "0,100,200,201|0,200,201", "100,0")]
    [InlineData("wf_xtra_mov_physics", "0,100,101,200,201,900|0,100,101,200,201,900", "900,900", "0,200,201,900", "900")]
    [InlineData("wf_xtra_mov_carry_users", "", "", "0,200,201,900", "900")]
    [InlineData("wf_trg_state_changed", "100,200", "100", "", "")]
    [InlineData("wf_trg_stuff_state", "100,200", "100", "", "")]
    [InlineData("wf_trg_bot_reached_avtr", "", "", "100,200", "100")]
    [InlineData("wf_act_change_var_val", "0,100,101,200,201|0,100,101,200,201", "0,200", "0,200,201|0,200,201", "0,200")]
    public void FreshEditorFramesOfferTheObservedSources(string name, string furni, string furniDefaults, string users, string userDefaults)
    {
        var metadata = WiredNativeEditorProjection.Metadata(name);

        Assert.Equal((furni, furniDefaults, users, userDefaults),
            (Groups(metadata.FurniAllowed), string.Join(",", metadata.FurniDefaults), Groups(metadata.UsersAllowed), string.Join(",", metadata.UserDefaults)));
    }

    [Theory]
    [InlineData("wf_act_match_to_sshot", "0,0,0,0")]
    [InlineData("wf_xtra_filter_furni", "10,0,1")]
    [InlineData("wf_cnd_slc_quantity", "0,5,0")]
    [InlineData("wf_act_send_signal", "0,0")]
    [InlineData("wf_act_bot_talk", "0,-1,0")]
    [InlineData("wf_act_change_var_val", "1,0,0,0,0,-10,0")]
    public void FreshEditorFramesCarryTheObservedDefaultInts(string name, string ints) =>
        Assert.Equal(ints, string.Join(",", WiredNativeEditorProjection.Metadata(name).OwnedDefaults));

    // Every trigger gives users 0/200/201, except Bot Reaches User (adds 10) and Click User (adds 11), inserted after 0 in every
    // user slot of every box in that stack; furni lists and defaults never change.
    [Fact]
    public void TheStackTriggerAddsTenOrElevenAfterZeroInEveryUserSlot()
    {
        Assert.Equal([10], WiredNativeEditorProjection.TriggerContextSources(["wf_trg_bot_reached_avtr", "wf_act_bot_talk"]).ToArray());
        Assert.Equal([11], WiredNativeEditorProjection.TriggerContextSources(["wf_trg_click_user", "wf_act_furni_to_user"]).ToArray());
        Assert.Empty(WiredNativeEditorProjection.TriggerContextSources(["wf_trg_says_something", "wf_act_furni_to_user"]));

        var talk = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata("wf_act_bot_talk"), [10]);
        Assert.Equal(("0,10,100,200,201", "100"), (Groups(talk.UsersAllowed), string.Join(",", talk.UserDefaults)));
        var match = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata("wf_cnd_triggerer_match"), [10]);
        Assert.Equal(("0,10,200,201|0,10,101,200,201", "0,101"), (Groups(match.UsersAllowed), string.Join(",", match.UserDefaults)));
        var toUser = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata("wf_act_furni_to_user"), [11]);
        Assert.Equal(("0,100,200,201", "0,11,200,201"), (Groups(toUser.FurniAllowed), Groups(toUser.UsersAllowed)));
        // A trigger's own user slot (no 0) is unchanged.
        var reached = WiredNativeEditorProjection.WithTriggerContext(WiredNativeEditorProjection.Metadata("wf_trg_bot_reached_avtr"), [10]);
        Assert.Equal("100,200", Groups(reached.UsersAllowed));
    }

    [Theory]
    [InlineData("wf_act_kick_user", 10)]
    [InlineData("wf_act_kick_user", 11)]
    [InlineData("wf_act_furni_to_user", 11)]
    [InlineData("wf_xtra_text_output_username", 11)]
    [InlineData("wf_act_bot_talk", 10)]
    public void AContextSourceSavesOnlyUnderItsTrigger(string name, int source)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var defaults = WiredNativeEditorProjection.DefaultNative(descriptor);
        var native = defaults with { UserSourceTypes = [source, .. defaults.UserSourceTypes.Skip(1)] };

        Assert.False(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _, [source]));
        Assert.False(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _, [source == 10 ? 11 : 10]));
    }

    // A row saved under the wider lists still loads; only a new save is checked against the current lists.
    [Fact]
    public void AStoredRowWithASourceTheEditorNoLongerOffersStillLoads()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_furni_bytype", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { FurniSourceTypes = [200] };

        Assert.False(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _, storedRow: true));
    }

    private static string Groups(ImmutableArray<ImmutableArray<int>> groups) => string.Join("|", groups.Select(group => string.Join(",", group)));
}
