using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Xunit;

namespace Plus.Tests;

public sealed class WiredEvidenceRegistryTests
{
    private static readonly string[] Expected =
    [
        "wf_cnd_not_wearing_b", "wf_cnd_not_wearing_fx", "wf_cnd_wearing_badge", "wf_cnd_wearing_effect",
        "wf_var_quest", "wf_var_quest_chain"
    ];

    [Fact]
    public void ExactlyTheSixAirSourceOnlyCardsAreMarked()
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
        "wf_act_bot_clothes", "wf_act_bot_follow_avatar", "wf_act_bot_give_handitem", "wf_act_bot_move", "wf_act_bot_talk",
        "wf_act_bot_talk_to_avatar", "wf_act_bot_teleport", "wf_act_move_furni_to", "wf_trg_at_time_long",
        "wf_trg_bot_reached_avtr", "wf_trg_bot_reached_stf", "wf_trg_game_ends", "wf_trg_game_starts"
    ];

    [Fact]
    public void ExactlyTheThirteenTurboReferenceOnlyCardsAreMarked()
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
        Assert.Equal(WiredEvidenceLevel.Observed, WiredEvidenceRegistry.LevelOf("wf_var_context"));
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
}
