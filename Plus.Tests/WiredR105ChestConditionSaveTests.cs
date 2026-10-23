using Plus.HabboHotel.Items.Wired.Chests;
using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Chest condition quantifier and user source as Habbo stores them.
public sealed class WiredR105ChestConditionSaveTests
{
    private static Result Run(string name, int? quantifier = null, int[]? users = null)
    {
        using var world = new G1World();
        var chest = world.AddChest(WiredChestKind.Furni).Id;

        return name == "wf_cnd_chest_has_items"
            ? Save(world, name, quantifier: quantifier, users: users, picks: [chest])
            : Save(world, name, quantifier: quantifier, users: users, secondary: [chest]);
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items", 0, 0)]
    [InlineData("wf_cnd_chest_has_items", 1, 1)]
    [InlineData("wf_cnd_chest_has_items", 2, 2)]
    [InlineData("wf_cnd_chest_has_items", -1, 0)]
    [InlineData("wf_cnd_chest_has_items", 5, 0)]
    [InlineData("wf_cnd_chest_has_item_type", 0, 0)]
    [InlineData("wf_cnd_chest_has_item_type", 1, 1)]
    [InlineData("wf_cnd_chest_has_item_type", 2, 2)]
    [InlineData("wf_cnd_chest_has_item_type", -1, 0)]
    [InlineData("wf_cnd_chest_has_item_type", 5, 0)]
    public void AQuantifierOutsideZeroToTwoIsStoredAsZero(string name, int sent, int stored)
    {
        var saved = Run(name, quantifier: sent);

        Assert.True(saved.Accepted, saved.ToString());
        Assert.Equal(stored, saved.Quantifier);
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void APickedUserSourceIsAnsweredWithTheUserSourceKey(string name) =>
        Assert.Equal("wiredfurni.error.invalid_user_source", Run(name, users: [100]).Key);
}
