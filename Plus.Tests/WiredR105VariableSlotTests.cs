using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Variable slots of the filter and scan add-ons as Habbo stores them. A save with no variable keeps the empty slot, a variable of
// another domain than the target is accepted, and more variables than slots are answered with the variable count key.
public sealed class WiredR105VariableSlotTests
{
    private const string CountKey = "wiredfurni.error.invalid_variable_count";

    private static Result Saved(string name, int[] ints, string[] variables)
    {
        var result = Save(name, ints, variables: variables);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', variables)}]: {Show(result)}");

        return result;
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableWithNoVariableKeepsBothEmptySlots(string name)
    {
        Assert.Equal(["0", "0"], Saved(name, [5, 0, 0, 1], []).Variables);
        Assert.Equal(["0", "0"], Saved(name, [5, 1, 1, 1], []).Variables);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableWithOneVariableKeepsTheEmptyOperandSlot(string name) =>
        Assert.Equal(["user:5", "0"], Saved(name, [5, 0, 0, 1], ["user:5"]).Variables);

    [Fact]
    public void FilterByVariableAcceptsAVariableOfAnotherDomain()
    {
        Assert.Equal(["user:5", "0"], Saved("wf_xtra_filter_furni_by_var", [5, 0, 0, 1], ["user:5", "0"]).Variables);
        Assert.Equal(["furni:5", "0"], Saved("wf_xtra_filter_users_by_var", [5, 0, 0, 1], ["furni:5", "0"]).Variables);
    }

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var", "furni:5")]
    [InlineData("wf_xtra_filter_users_by_var", "user:5")]
    public void FilterByVariableAcceptsAnOperandThatDoesNotFitItsTarget(string name, string variable) =>
        Assert.Equal([variable, "user:7"], Saved(name, [5, 0, 1, 0], [variable, "user:7"]).Variables);

    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var")]
    [InlineData("wf_xtra_filter_users_by_var")]
    public void FilterByVariableWithMoreThanTwoVariablesIsAnsweredWithTheCountKey(string name) =>
        Assert.Equal(CountKey, Save(name, [5, 0, 0, 1], variables: ["user:5", "furni:6", "user:7"]).Key);

    [Fact]
    public void FilterFurniInVariableModeWithNoVariableKeepsTheEmptySlot() =>
        Assert.Equal(["0"], Saved("wf_xtra_filter_furni", [5, 1, 0], []).Variables);

    [Fact]
    public void ScanChestWithNoVariableKeepsTheEmptySlot() => Assert.Equal(["0"], Saved("wf_xtra_scan_chest_furni_by_type", [1], []).Variables);

    [Theory]
    [InlineData("wf_xtra_filter_furni")]
    [InlineData("wf_xtra_scan_chest_furni_by_type")]
    public void AddonsWithOneVariableSlotAnswerTwoVariablesWithTheCountKey(string name) =>
        Assert.Equal(CountKey, Save(name, name == "wf_xtra_filter_furni" ? [5, 1, 0] : [1], variables: ["user:5", "furni:6"]).Key);
}
