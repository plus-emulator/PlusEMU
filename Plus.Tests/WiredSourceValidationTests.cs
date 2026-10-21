using Xunit;

namespace Plus.Tests;

// The answer to a source list a save should not carry, the same for every box with a native editor. A source value Habbo does not know
// is dropped without any reply; a known value the slot does not offer gets the furni or user source key; a list longer than the box has
// slots gets the count key; the values are checked before the counts. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredSourceValidationTests
{
    private const string FurniSource = "wiredfurni.error.invalid_furni_source";
    private const string FurniSourceCount = "wiredfurni.error.invalid_furni_source_count";
    private const string UserSource = "wiredfurni.error.invalid_user_source";
    private const string UserSourceCount = "wiredfurni.error.invalid_user_source_count";
    private const string Dropped = "";

    public static TheoryData<string, int[], int[]?, int[]?, string> Refusals => new()
    {
        // A furni source value Habbo does not know is dropped; a known value the slot does not offer has its key.
        { "wf_act_flee", [], [300], null, Dropped },
        { "wf_act_flee", [], [11], null, Dropped },
        { "wf_act_teleport_to_room", [], [5], null, Dropped },
        { "wf_act_flee", [], [101], null, FurniSource },
        { "wf_act_flee", [], [901], null, FurniSource },
        { "wf_slc_furni_bytype", [0, 0], [200], null, FurniSource },
        { "wf_trg_click_tile", [], [0], null, FurniSource },
        // The value is checked before the count of the list.
        { "wf_cnd_slc_quantity", [0, 2, 1], [100, 200], null, FurniSource },
        { "wf_cnd_slc_quantity", [0, 2, 1], [901, 200], null, FurniSource },
        // A user source value Habbo does not know is dropped, also on a box without user slots.
        { "wf_act_flee", [], null, [300], Dropped },
        // A user source the slot does not offer, on any box.
        { "wf_cnd_actor_in_group", [], null, [100], UserSource },
        { "wf_cnd_not_in_group", [], null, [100], UserSource },
        { "wf_cnd_has_handitem", [2], null, [100], UserSource },
        { "wf_cnd_not_has_handitem", [2], null, [100], UserSource },
        { "wf_cnd_slc_quantity", [0, 2, 1], null, [100], UserSource },
        { "wf_cnd_team_has_rank", [1, 1], null, [100], UserSource },
        { "wf_cnd_team_has_score", [1, 5, 1], null, [100], UserSource },
        { "wf_cnd_trggrer_on_frn", [], null, [100], UserSource },
        { "wf_act_move_furni_as_group", [1, 0, 0], null, [100], UserSource },
        // A trigger has no trigger context: it refuses the context sources and the ones it does not offer.
        { "wf_trg_bot_reached_avtr", [], null, [0], UserSource },
        { "wf_trg_bot_reached_avtr", [], null, [10], UserSource },
        { "wf_trg_bot_reached_avtr", [], null, [11], UserSource },
        { "wf_trg_bot_reached_avtr", [], null, [201], UserSource },
        // More user sources than the box has slots.
        { "wf_act_flee", [], null, [0], UserSourceCount },
        { "wf_act_move_furni_as_group", [0, 1, 1], null, [0, 0], UserSourceCount },
        { "wf_act_neg_send_signal", [0, 0], null, [0, 200], UserSourceCount },
        { "wf_act_neg_send_signal", [0, 0], null, [200, 200], UserSourceCount },
        { "wf_act_teleport_to_room", [], null, [0, 200], UserSourceCount },
        { "wf_act_teleport_to_room", [], null, [0, 0], UserSourceCount },
        { "wf_trg_bot_reached_avtr", [], null, [100, 200], UserSourceCount },
        { "wf_trg_bot_reached_avtr", [], null, [100, 100], UserSourceCount },
        // More furni sources than the box has slots.
        { "wf_act_flee", [], [100, 100], null, FurniSourceCount },
        { "wf_cnd_has_handitem", [2], [100], null, FurniSourceCount }
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public void ASourceListTheBoxCannotTakeIsAnsweredLikeHabbo(string name, int[] ints, int[]? furni, int[]? users, string expected)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: ints, furni: furni, users: users);

        if (expected == Dropped) {
            Assert.Equal("none", result.Answer);
        }
        else {
            Assert.True(result.Answer == "error" && result.Key == expected, $"{name} furni=[{string.Join(',', furni ?? [])}] users=[{string.Join(',', users ?? [])}]: {result}");
        }
    }

    [Theory]
    [InlineData("wf_cnd_actor_in_group", new int[0], 10)]
    [InlineData("wf_cnd_actor_in_group", new int[0], 11)]
    [InlineData("wf_cnd_has_handitem", new[] { 2 }, 10)]
    [InlineData("wf_cnd_team_has_rank", new[] { 1, 1 }, 11)]
    [InlineData("wf_cnd_slc_quantity", new[] { 0, 2, 1 }, 10)]
    [InlineData("wf_act_move_furni_as_group", new[] { 1, 0, 0 }, 11)]
    public void ATriggerContextUserSourceIsKeptOnAnyBoxAboveTheTriggers(string name, int[] ints, int source)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: ints, users: [source]);

        Assert.True(result.Accepted, $"{name} {source}: {result}");
        Assert.Equal([source], result.Users);
    }
}
