using Xunit;

namespace Plus.Tests;

// Has Variable and Not Has Variable saves as Habbo stores them. A save may name no variable and may name a variable of any domain
// with any target. An unoffered target is stored as 0. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredHasVariableSaveTests
{
    public static TheoryData<string> Boxes => new() { "wf_cnd_has_var", "wf_cnd_neg_has_var" };

    [Theory]
    [MemberData(nameof(Boxes))]
    public void ASaveWithNoVariableIsStoredWithTheEmptySlotForEveryOfferedTarget(string name)
    {
        foreach (var target in new[] { 1, 0, -20, -10 }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [target], variables: []);

            Assert.True(result.Accepted, $"{name} target {target}: {result}");
            Assert.Equal([target], result.Ints);
            Assert.Equal(["0"], result.Variables);
        }
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void ATargetTheEditorDoesNotOfferIsStoredAsZero(string name)
    {
        foreach (var target in new[] { 2, 3, 11, 100, -1, int.MaxValue, int.MinValue }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [target], variables: []);

            Assert.True(result.Accepted, $"{name} target {target}: {result}");
            Assert.Equal([0], result.Ints);
        }
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void AVariableOfAnyDomainIsStoredWithAnyOfferedTarget(string name)
    {
        foreach (var variable in new[] { "user:5", "furni:5", "ctx:5", "room:5" }) {
            foreach (var target in new[] { 1, 0, -20, -10 }) {
                using var save = new WiredRawSave(name);
                var result = save.Save(ints: [target], variables: [variable]);

                Assert.True(result.Accepted, $"{name} {variable} target {target}: {result}");
                Assert.Equal([target], result.Ints);
                Assert.Equal([variable], result.Variables);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void AnEmptyIntListTakesTheFrameDefault(string name)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: [], variables: []);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([1], result.Ints);
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void TwoVariablesAreRefusedWithTheVariableCountKey(string name)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: [1], variables: ["user:5", "furni:5"]);

        Assert.Equal("error", result.Answer);
        Assert.Equal("wiredfurni.error.invalid_variable_count", result.Key);
    }
}
