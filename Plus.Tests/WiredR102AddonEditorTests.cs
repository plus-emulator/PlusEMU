using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Editor saves of the add-ons, read back the way Habbo stores them.
public sealed class WiredR102AddonEditorTests
{
    private const string UserSourceKey = "wiredfurni.error.invalid_user_source";

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(50000, 100)]
    [InlineData(int.MaxValue, 100)]
    public void ExecutionLimitStoresTheCountBetweenOneAndOneHundred(int sent, int stored) =>
        Assert.Equal([stored, 1], Saved("wf_xtra_execution_limit", [sent, 1]).Ints);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(int.MinValue, 1)]
    [InlineData(20, 20)]
    [InlineData(21, 21)]
    [InlineData(40, 40)]
    [InlineData(100, 100)]
    [InlineData(120, 120)]
    [InlineData(1000, 120)]
    [InlineData(100000, 120)]
    [InlineData(int.MaxValue, 120)]
    public void ExecutionLimitStoresTheWindowOfUpToOneHundredAndTwentyPulses(int sent, int stored) =>
        Assert.Equal([3, stored], Saved("wf_xtra_execution_limit", [3, sent]).Ints);

    [Fact]
    public void ExecutionLimitWindowOfFortyPulsesRunsAsTwentySeconds()
    {
        Assert.Equal([3, 20000], Saved("wf_xtra_execution_limit", [3, 40]).RuntimeInts);
        Assert.Equal([3, 60000], Saved("wf_xtra_execution_limit", [3, 1000]).RuntimeInts);
        var again = WiredAddonConfiguration.Normalize("wf_xtra_execution_limit", new() { IntParams = [3, 60000] });
        Assert.Equal([3, 60000], again.IntParams.ToArray());
    }

    [Fact]
    public void ExecutionLimitFillsMissingIntsDropsExtraOnesAndIgnoresText()
    {
        Assert.Equal([7, 1], Saved("wf_xtra_execution_limit", [7]).Ints);
        Assert.Equal([100, 1], Saved("wf_xtra_execution_limit", []).Ints);
        Assert.Equal([3, 4], Saved("wf_xtra_execution_limit", [3, 4, 9]).Ints);
        var withText = Saved("wf_xtra_execution_limit", [3, 4], text: "abc");
        Assert.Equal([3, 4], withText.Ints);
        Assert.Equal("", withText.Text);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 1)]
    [InlineData(1000, 1000)]
    [InlineData(1001, 1000)]
    public void FilterFurniStoresTheAmountBetweenOneAndOneThousand(int sent, int stored) =>
        Assert.Equal([stored, 0, 1], Saved("wf_xtra_filter_furni", [sent, 0, 1]).Ints);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void FilterFurniReadsTheOperandKindTwoAsOne(int sent, int stored) =>
        Assert.Equal([5, stored, 1], Saved("wf_xtra_filter_furni", [5, sent, 1]).Ints);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-10, -10)]
    [InlineData(-20, -20)]
    [InlineData(2, 0)]
    [InlineData(99, 0)]
    [InlineData(-5, 0)]
    public void FilterFurniStoresAnUnknownReferenceTargetAsZero(int sent, int stored) =>
        Assert.Equal([5, 0, stored], Saved("wf_xtra_filter_furni", [5, 0, sent]).Ints);

    [Fact]
    public void FilterFurniFillsMissingIntsFromTheFrameAndDropsExtraOnes()
    {
        Assert.Equal([5, 0, 1], Saved("wf_xtra_filter_furni", [5]).Ints);
        Assert.Equal([10, 0, 1], Saved("wf_xtra_filter_furni", []).Ints);
        Assert.Equal([5, 0, 1], Saved("wf_xtra_filter_furni", [5, 0, 1, 7]).Ints);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni")]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterAddonsRefuseUserSourcesTheyDoNotOfferWithTheUserSourceKey(string name)
    {
        var ints = name == "wf_xtra_filter_furni" ? new[] { 10, 0, 1 } : [10, 0, 0, 1];
        Assert.Equal(UserSourceKey, Refused(name, ints, users: [100]));
        Assert.Equal(UserSourceKey, Refused(name, ints, users: [200]));
        Assert.Equal([201], Saved(name, ints, users: [201]).Users);
        Assert.Equal([0], Saved(name, ints, users: [0]).Users);
        Assert.Equal("wiredfurni.error.invalid_furni_source", Refused(name, ints, furni: [101]));
        Assert.Equal("wiredfurni.error.invalid_furni_source", Refused(name, ints, furni: [200]));
        Assert.Equal("wiredfurni.error.invalid_furni_source", Refused(name, ints, furni: [901]));
        Assert.Equal([201], Saved(name, ints, furni: [201]).Furni);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableStoresTheSortOrderZeroToFiveAndTheRestAsZero(string name)
    {
        foreach (var order in new[] { 0, 1, 2, 3, 4, 5 }) {
            Assert.Equal([5, order, 0, 1], Saved(name, [5, order, 0, 1]).Ints);
        }

        Assert.Equal([5, 0, 0, 1], Saved(name, [5, 6, 0, 1]).Ints);
        Assert.Equal([5, 0, 0, 1], Saved(name, [5, -1, 0, 1]).Ints);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableStoresTheAmountTheOperandKindAndTheTarget(string name)
    {
        Assert.Equal([1, 0, 0, 1], Saved(name, [0, 0, 0, 1]).Ints);
        Assert.Equal([1, 0, 0, 1], Saved(name, [-1, 0, 0, 1]).Ints);
        Assert.Equal([1000, 0, 0, 1], Saved(name, [1001, 0, 0, 1]).Ints);
        Assert.Equal([5, 0, 1, 1], Saved(name, [5, 0, 2, 1]).Ints);

        foreach (var kept in new[] { 0, 1, -10, -20 }) {
            Assert.Equal([5, 0, 0, kept], Saved(name, [5, 0, 0, kept]).Ints);
        }

        Assert.Equal([5, 0, 0, 0], Saved(name, [5, 0, 0, 2]).Ints);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableFillsMissingIntsFromTheFrameAndDropsExtraOnes(string name)
    {
        Assert.Equal([5, 0, 0, 1], Saved(name, [5, 0, 0]).Ints);
        Assert.Equal([5, 0, 0, 1], Saved(name, [5]).Ints);
        Assert.Equal([10, 0, 0, 1], Saved(name, []).Ints);
        Assert.Equal([5, 0, 0, 1], Saved(name, [5, 0, 0, 1, 9]).Ints);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(-1, -1)]
    [InlineData(4, 3)]
    [InlineData(9, 3)]
    [InlineData(-2, -1)]
    public void ConditionEvaluationStoresTheModeBetweenMinusOneAndThree(int sent, int stored) =>
        Assert.Equal(stored, Saved("wf_xtra_or_eval", [sent, 0, 0]).Ints[0]);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConditionEvaluationStoresEveryComparisonUpToThree(int comparison) =>
        Assert.Equal([-1, comparison, 5], Saved("wf_xtra_or_eval", [-1, comparison, 5]).Ints);

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    [InlineData(-3)]
    public void ConditionEvaluationStoresTheCountAsSent(int count) =>
        Assert.Equal([-1, 1, count], Saved("wf_xtra_or_eval", [-1, 1, count]).Ints);

    [Fact]
    public void ConditionEvaluationKeepsTheComparisonAndCountOfAPlainMode()
    {
        Assert.Equal([1, 1, 7], Saved("wf_xtra_or_eval", [1, 1, 7]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_xtra_or_eval", [1]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_xtra_or_eval", []).Ints);
        Assert.Equal([-1, 1, 5], Saved("wf_xtra_or_eval", [-1, 1, 5, 9]).Ints);
    }

    [Fact]
    public void ConditionEvaluationSourcesKeepTheirKeys()
    {
        Assert.Equal("wiredfurni.error.invalid_furni_source", Refused("wf_xtra_or_eval", [1, 0, 0], furni: [101]));
        Assert.Equal("wiredfurni.error.invalid_user_source_count", Refused("wf_xtra_or_eval", [1, 0, 0], users: [0]));
        Assert.Equal("wiredfurni.error.invalid_user_source_count", Refused("wf_xtra_or_eval", [1, 0, 0], users: [200]));

        foreach (var source in new[] { 0, 100, 200, 201 }) {
            Assert.Equal([source], Saved("wf_xtra_or_eval", [1, 0, 0], furni: [source]).Furni);
        }
    }

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 1 })]
    [InlineData(new[] { 0 })]
    [InlineData(new[] { 5, 6 })]
    [InlineData(new[] { -1 })]
    public void UnseenIgnoresEveryIntAndTheText(int[] ints)
    {
        var saved = Saved("wf_xtra_unseen", ints, text: "abc");
        Assert.Empty(saved.Ints);
        Assert.Equal("", saved.Text);
        Assert.Equal("wiredfurni.error.invalid_furni_source_count", Refused("wf_xtra_unseen", [], furni: [100]));
        Assert.Equal("wiredfurni.error.invalid_user_source_count", Refused("wf_xtra_unseen", [], users: [0]));
    }

    private static Result Saved(string name, int[] ints, int[]? furni = null, int[]? users = null, string? text = null)
    {
        var result = Save(name, ints, furni, users, text);
        Assert.True(result.Accepted, Show(result));

        return result;
    }

    private static string? Refused(string name, int[] ints, int[]? furni = null, int[]? users = null)
    {
        var result = Save(name, ints, furni, users);
        Assert.False(result.Accepted, Show(result));
        Assert.False(result.Dropped);

        return result.Key;
    }
}
