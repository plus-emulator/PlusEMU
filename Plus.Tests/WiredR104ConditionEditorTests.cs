using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Condition editor saves as Habbo stores them: out-of-range values are clamped or reset, not refused.
public sealed class WiredR104ConditionEditorTests
{
    private static Result Saved(string name, int[]? ints = null, string? text = null, int? quantifier = null)
    {
        using var world = new G1World();
        var result = Save(world, name, ints, text: text, quantifier: quantifier);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints ?? [])}] q={quantifier}: {result}");

        return result;
    }

    [Fact]
    public void SelectionQuantityKeepsAFlagOfZeroOrOneAndClampsTheAmountToZeroToOneHundred()
    {
        const string name = "wf_cnd_slc_quantity";

        Assert.Equal([0, 2, 1], Saved(name, [0, 2, 1]).Ints);
        Assert.Equal([1, 2, 1], Saved(name, [1, 2, 1]).Ints);

        foreach (var flag in new[] { 2, -1, 100, int.MinValue }) {
            Assert.Equal([0, 2, 1], Saved(name, [flag, 2, 1]).Ints);
        }

        foreach (var amount in new[] { 101, 1000, int.MaxValue }) {
            Assert.Equal([0, 100, 1], Saved(name, [0, amount, 1]).Ints);
        }

        Assert.Equal([0, 100, 1], Saved(name, [0, 100, 1]).Ints);
        Assert.Equal([0, 0, 1], Saved(name, [0, 0, 1]).Ints);
        Assert.Equal([0, 0, 1], Saved(name, [0, -1, 1]).Ints);
    }

    [Fact]
    public void SelectionQuantityStoresComparisonsThreeAndFourAndResetsANegativeOne()
    {
        const string name = "wf_cnd_slc_quantity";

        foreach (var comparison in new[] { 0, 1, 2, 3, 4 }) {
            Assert.Equal([0, 2, comparison], Saved(name, [0, 2, comparison]).Ints);
        }

        Assert.Equal([0, 2, 0], Saved(name, [0, 2, -1]).Ints);
        Assert.Equal([0, 5, 0], Saved(name, [5]).Ints);
        Assert.Equal([0, 5, 0], Saved(name, []).Ints);
        Assert.Equal([0, 2, 1], Saved(name, [0, 2, 1, 9]).Ints);
    }

    [Fact]
    public void TeamRankClampsTheTeamToZeroToFourAndThePlacementToZeroToThree()
    {
        const string name = "wf_cnd_team_has_rank";

        foreach (var team in new[] { 0, 1, 2, 3, 4 }) {
            Assert.Equal([team, 1], Saved(name, [team, 1]).Ints);
        }

        foreach (var team in new[] { 5, 100, int.MaxValue }) {
            Assert.Equal([4, 1], Saved(name, [team, 1]).Ints);
        }

        Assert.Equal([0, 1], Saved(name, [-1, 1]).Ints);

        foreach (var placement in new[] { 4, 100, int.MaxValue }) {
            Assert.Equal([1, 3], Saved(name, [1, placement]).Ints);
        }

        Assert.Equal([1, 0], Saved(name, [1, -1]).Ints);
        Assert.Equal([1, 0], Saved(name, [1]).Ints);
        Assert.Equal([0, 0], Saved(name, []).Ints);
    }

    [Fact]
    public void TeamScoreClampsTheScoreToZeroTo1000AndStoresComparisonsThreeAndFour()
    {
        const string name = "wf_cnd_team_has_score";

        foreach (var team in new[] { 5, 100, int.MaxValue }) {
            Assert.Equal([4, 4, 2], Saved(name, [team, 4, 2]).Ints);
        }

        Assert.Equal([0, 4, 2], Saved(name, [-1, 4, 2]).Ints);

        foreach (var score in new[] { 1001, 100000, int.MaxValue }) {
            Assert.Equal([1, 1000, 2], Saved(name, [1, score, 2]).Ints);
        }

        Assert.Equal([1, 1000, 2], Saved(name, [1, 1000, 2]).Ints);
        Assert.Equal([1, 0, 2], Saved(name, [1, -1, 2]).Ints);

        foreach (var comparison in new[] { 0, 1, 2, 3, 4 }) {
            Assert.Equal([1, 4, comparison], Saved(name, [1, 4, comparison]).Ints);
        }

        Assert.Equal([1, 4, 0], Saved(name, [1, 4, -1]).Ints);
        Assert.Equal([1, 0, 0], Saved(name, [1]).Ints);
        Assert.Equal([0, 0, 0], Saved(name, []).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_time_less_than")]
    [InlineData("wf_cnd_time_more_than")]
    public void TheTimeConditionsStoreAnyThresholdAsSent(string name)
    {
        foreach (var threshold in new[] { 0, 1, 2, 1200, 1201, 1202, 100000, -1, -100, int.MaxValue, int.MinValue }) {
            Assert.Equal([threshold], Saved(name, [threshold]).Ints);
        }

        Assert.Equal([20], Saved(name, []).Ints);
        Assert.Equal([3], Saved(name, [3, 4]).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_actor_in_group")]
    [InlineData("wf_cnd_not_in_group")]
    public void TheGroupIdIsKeptWhenItIsADecimalNumberAndClearedOtherwise(string name)
    {
        foreach (var kept in new[] { "", "0", "1", "-1", "123456789", "99999999999999" }) {
            Assert.Equal(kept, Saved(name, [], kept).Text);
        }

        foreach (var cleared in new[] { "abc", " 5 ", "1,2", new string('1', 100) + "x" }) {
            Assert.Equal("", Saved(name, [], cleared).Text);
        }
    }

    [Theory]
    [InlineData("wf_cnd_slc_quantity")]
    [InlineData("wf_cnd_team_has_rank")]
    [InlineData("wf_cnd_team_has_score")]
    [InlineData("wf_cnd_time_less_than")]
    [InlineData("wf_cnd_time_more_than")]
    [InlineData("wf_cnd_trggrer_on_frn")]
    [InlineData("wf_cnd_actor_in_group")]
    [InlineData("wf_cnd_not_in_group")]
    public void TheConditionQuantifierKeepsZeroOneAndTwoAndResetsEverythingElse(string name)
    {
        foreach (var quantifier in new[] { 0, 1, 2 }) {
            Assert.Equal(quantifier, Saved(name, quantifier: quantifier).Quantifier);
        }

        foreach (var quantifier in new[] { -1, 5, 100, int.MinValue }) {
            Assert.Equal(0, Saved(name, quantifier: quantifier).Quantifier);
        }
    }
}
