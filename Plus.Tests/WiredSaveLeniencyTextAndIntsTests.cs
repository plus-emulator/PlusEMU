using Xunit;

namespace Plus.Tests;

// A save with a text or ints the box does not read is accepted and the extra part is dropped.
public sealed class WiredSaveLeniencyTextAndIntsTests
{
    // Boxes with no int fields and no text field.
    private const string BoxesWithNoFields = "wf_act_chase,wf_act_flee,wf_act_neg_call_stacks,wf_act_toggle_to_rnd,wf_act_reset_timers,wf_act_unfreeze,"
        + "wf_cnd_stuff_is,wf_cnd_not_stuff_is,wf_cnd_trggrer_on_frn,wf_cnd_not_trggrer_on,wf_trg_collision,wf_trg_state_changed,"
        + "wf_trg_walks_off_furni,wf_trg_click_tile,wf_slc_users_onfurni,wf_slc_furni_signal";

    // Boxes that have int fields but no text field.
    private const string BoxesWithIntsOnly = "wf_cnd_actor_dir,wf_cnd_actor_in_team,wf_cnd_not_in_team,wf_cnd_counter_time_matches,wf_cnd_furnis_hv_avtrs,"
        + "wf_cnd_not_furni_on,wf_cnd_not_hv_avtrs,wf_cnd_has_altitude,wf_cnd_not_user_count,wf_cnd_slc_quantity,wf_cnd_team_has_rank,"
        + "wf_cnd_team_has_score,wf_cnd_time_less_than,wf_cnd_time_more_than,wf_cnd_has_handitem,wf_cnd_not_has_handitem,wf_act_move_furni_as_group,"
        + "wf_act_neg_send_signal,wf_trg_click_user,wf_trg_clock_counter";

    public static TheoryData<string> NoFields => [.. BoxesWithNoFields.Split(',')];

    public static TheoryData<string> IntsOnly => [.. BoxesWithIntsOnly.Split(',')];

    public static TheoryData<string> NoText => [.. (BoxesWithNoFields + "," + BoxesWithIntsOnly).Split(',')];

    // A pick the box accepts: a counter, an antenna or a click tile where the editor insists on one.
    private static uint[] Picks(WiredRawSave save, string name) => name switch
    {
        "wf_trg_clock_counter" or "wf_cnd_counter_time_matches" => [save.Furni("wf_upcounter1")],
        "wf_trg_click_tile" => [save.Furni("room_invisible_click_tile")],
        _ => []
    };

    [Theory]
    [MemberData(nameof(NoText))]
    public void ATextTheBoxDoesNotReadIsDropped(string name)
    {
        using var save = new WiredRawSave(name);
        var before = save.Save(picks: Picks(save, name));
        Assert.True(before.Accepted, $"{name} plain save: {before}");

        var result = save.Save(ints: before.Ints, text: "abc", picks: Picks(save, name));

        Assert.True(result.Accepted, $"{name} with a text: {result}");
        Assert.Equal("", result.Text);
        Assert.Equal(before.Ints, result.Ints);
    }

    [Theory]
    [MemberData(nameof(NoFields))]
    public void IntsABoxWithoutIntFieldsDoesNotReadAreDropped(string name)
    {
        using var save = new WiredRawSave(name);

        foreach (var ints in new[] { new[] { 1, 2 }, [7], [-1, 0, 5] }) {
            var result = save.Save(ints: ints, picks: Picks(save, name));

            Assert.True(result.Accepted, $"{name} ints [{string.Join(',', ints)}]: {result}");
            Assert.Empty(result.Ints);
        }
    }

    [Theory]
    [MemberData(nameof(IntsOnly))]
    public void ExtraIntsAreDroppedAndTheFieldsKeepTheirValues(string name)
    {
        using var save = new WiredRawSave(name);
        var before = save.Save(picks: Picks(save, name));
        Assert.True(before.Accepted, $"{name} plain save: {before}");

        var result = save.Save(ints: [.. before.Ints, 1, 2], picks: Picks(save, name));

        Assert.True(result.Accepted, $"{name} with extra ints: {result}");
        Assert.Equal(before.Ints, result.Ints);
    }
}
