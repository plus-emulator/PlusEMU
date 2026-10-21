using Plus.HabboHotel.Items.Wired.Chests;
using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// The chest conditions take the variable rules of the other variable boxes. Observed on AIR WIN63-202610201032-107964540: a save with no
// variable keeps the empty slot, a variable of another domain than the target is accepted, and a second variable is answered with the
// variable count key.
public sealed class WiredR105ChestConditionVariableTests
{
    private static Result Saved(string name, int[] ints, string[] variables, bool variableMode = true)
    {
        using var world = new G1World();
        var chest = world.AddChest(WiredChestKind.Furni).Id;
        var result = name == "wf_cnd_chest_has_items"
            ? Save(world, name, ints, variables: variables, picks: [chest])
            : Save(world, name, ints, variables: variables, secondary: [chest]);
        Assert.True(result.Accepted || !variableMode, $"{name}: {result}");

        return result;
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void ASaveWithNoVariableIsAccepted(string name) => Assert.True(Saved(name, [3, 1, 1, 2], []).Accepted);

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void AVariableOfAnotherDomainThanTheTargetIsAccepted(string name)
    {
        Assert.True(Saved(name, [3, 1, 1, 2], ["furni:5"]).Accepted);
        Assert.True(Saved(name, [3, 1, 0, 2], ["user:5"]).Accepted);
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void TwoVariablesAreAnsweredWithTheVariableCountKey(string name)
    {
        using var world = new G1World();
        var chest = world.AddChest(WiredChestKind.Furni).Id;
        var result = name == "wf_cnd_chest_has_items"
            ? Save(world, name, [3, 1, 1, 2], variables: ["user:5", "user:6"], picks: [chest])
            : Save(world, name, [3, 1, 1, 2], variables: ["user:5", "user:6"], secondary: [chest]);

        Assert.Equal("wiredfurni.error.invalid_variable_count", result.Key);
    }
}
