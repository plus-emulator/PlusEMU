using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Xunit;

namespace Plus.Tests;

public sealed class WiredEvidenceRegistryTests
{
    // The six October conditions/variables of r61, then the 22 boxes of the r88 dossier.
    private static readonly string[] Expected =
    [
        "wf_act_give_effect", "wf_act_override_height", "wf_act_progress_ach", "wf_act_progress_reward_track", "wf_act_reset_reward_track",
        "wf_act_send_dataframe", "wf_act_send_message", "wf_act_set_highlight", "wf_act_set_visibility",
        "wf_cnd_not_wearing_b", "wf_cnd_not_wearing_fx", "wf_cnd_user_level", "wf_cnd_wearing_badge", "wf_cnd_wearing_effect",
        "wf_trg_receive_dataframe", "wf_trg_receive_message", "wf_trg_room_initializes",
        "wf_var_dailytask", "wf_var_pointer", "wf_var_quest", "wf_var_quest_chain",
        "wf_xtra_achievement_enabler", "wf_xtra_global_placeholder", "wf_xtra_link_speaker", "wf_xtra_var_import",
        "wf_xtra_var_share", "wf_xtra_var_web_api"
    ];

    [Fact]
    public void ExactlyTheAirSourceOnlyCardsAreMarked()
    {
        Assert.Equal(Expected, WiredEvidenceRegistry.AirSourceOnly.Keys.Order(StringComparer.Ordinal));
        Assert.All(WiredEvidenceRegistry.AirSourceOnly.Values, evidence => {
            Assert.Equal(WiredEvidenceLevel.AirSourceOnly, evidence.Level);
            Assert.EndsWith(".as", evidence.Source);
        });

        var marked = Expected.Where(name => WiredEvidenceRegistry.LevelOf(name) == WiredEvidenceLevel.AirSourceOnly);
        Assert.Equal(Expected.Length, marked.Count());
    }

    private static readonly string[] ExpectedTurbo =
    [
        "wf_act_bot_clothes", "wf_act_bot_follow_avatar", "wf_act_bot_give_handitem",
        "wf_act_bot_move", "wf_act_bot_talk", "wf_act_bot_talk_to_avatar",
        "wf_act_bot_teleport", "wf_act_give_currency",
        "wf_act_give_furni", "wf_act_give_reward", "wf_act_move_furni_to",
        "wf_act_mute_triggerer", "wf_act_place_furni", "wf_act_remove_furni",
        "wf_cnd_chest_has_item_type", "wf_cnd_chest_has_items", "wf_trg_at_time_long",
        "wf_trg_bot_reached_avtr", "wf_trg_bot_reached_stf", "wf_trg_transaction_complete",
        "wf_var_reference", "wf_xtra_scan_chest_furni_by_type", "wf_xtra_var_time_util"
    ];

    [Fact]
    public void ExactlyTheTurboReferenceOnlyCardsAreMarked()
    {
        Assert.Equal(ExpectedTurbo, WiredEvidenceRegistry.TurboReferenceOnly.Keys.Order(StringComparer.Ordinal));
        Assert.All(WiredEvidenceRegistry.TurboReferenceOnly.Values, evidence => {
            Assert.Equal(WiredEvidenceLevel.TurboReferenceOnly, evidence.Level);
            Assert.EndsWith(".as", evidence.Source);
        });
        Assert.All(ExpectedTurbo, name => {
            Assert.Equal(WiredEvidenceLevel.TurboReferenceOnly, WiredEvidenceRegistry.LevelOf(name));
            Assert.True(WiredBoxRegistry.TryGet(name, out _), name);
        });
        Assert.Empty(ExpectedTurbo.Intersect(WiredEvidenceRegistry.AirSourceOnly.Keys));
    }

    [Fact]
    public void TheR90CardsAreObservedWithTheirBatchSummary()
    {
        Assert.Equal(17, WiredEvidenceRegistry.ObservedR90.Count);
        Assert.All(WiredEvidenceRegistry.ObservedR90, pair =>
        {
            Assert.Equal(WiredEvidenceLevel.Observed, pair.Value.Level);
            Assert.Equal(WiredEvidenceLevel.Observed, WiredEvidenceRegistry.LevelOf(pair.Key));
            Assert.StartsWith("reviews/october-batch-r90-r90-", pair.Value.Source);
            Assert.True(WiredBoxRegistry.TryGet(pair.Key, out _), pair.Key);
        });
        Assert.False(WiredBoxRegistry.TryGet("wf_label", out _));
        Assert.StartsWith("reviews/october-batch-r90-r90-", WiredEvidenceRegistry.LabelFurniR90.Source);
    }

    [Fact]
    public void EveryMarkedCardIsARegisteredBox()
    {
        foreach (var name in Expected) {
            Assert.True(WiredBoxRegistry.TryGet(name, out _), name);
        }
    }

    [Fact]
    public void OtherCardsStayObserved()
    {
        Assert.Equal(WiredEvidenceLevel.Observed, WiredEvidenceRegistry.LevelOf("wf_cnd_actor_in_group"));
        Assert.Equal(WiredEvidenceLevel.Observed, WiredEvidenceRegistry.LevelOf("wf_act_move_rotate"));
    }

    [Theory]
    [InlineData("wf_cnd_wearing_effect", new[] { -2147483648, 0, 1 }, "")]
    [InlineData("wf_cnd_not_wearing_fx", new[] { 2147483647, 0, 1 }, "")]
    [InlineData("wf_cnd_wearing_badge", new[] { 0, 0 }, "ACH_Login1")]
    [InlineData("wf_cnd_not_wearing_b", new[] { 0, 0 }, "")]
    public void AirEditorRangesAreAccepted(string name, int[] ints, string text)
    {
        Assert.True(WiredConditionConfiguration.TryValidate(name, new() { IntParams = [.. ints], Text = text }, out _, out _));
    }

    [Fact]
    public void BadgeCodeBoxTakesUpToAThousandCharacters()
    {
        Assert.True(WiredConditionConfiguration.TryValidate("wf_cnd_wearing_badge", new() { IntParams = [0, 0], Text = new string('a', 1000) }, out _, out _));
        Assert.False(WiredConditionConfiguration.TryValidate("wf_cnd_wearing_badge", new() { IntParams = [0, 0], Text = new string('a', 1001) }, out _, out _));
    }

    [Fact]
    public void R92ObservedMovementCardsAreNotListedAsUnobserved()
    {
        Assert.Equal(19, WiredEvidenceRegistry.ObservedR92.Count);
        Assert.All(WiredEvidenceRegistry.ObservedR92, pair =>
        {
            Assert.True(WiredBoxRegistry.TryGet(pair.Key, out _), pair.Key);
            Assert.Equal(WiredEvidenceLevel.Observed, WiredEvidenceRegistry.LevelOf(pair.Key));
            Assert.StartsWith("reviews/r92-movement-facts.md#", pair.Value.Source);
        });
    }
}
