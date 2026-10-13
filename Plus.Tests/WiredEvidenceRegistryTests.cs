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
