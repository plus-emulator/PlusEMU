using Plus.HabboHotel.Items.Wired.Chests;
using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Chest condition saves as Habbo stores them.
public sealed class WiredR104ChestConditionTests
{
    private static Result Saved(string name, int[] ints)
    {
        using var world = new G1World();
        var chest = world.AddChest(WiredChestKind.Furni).Id;
        var result = name == "wf_cnd_chest_has_items" ? Save(world, name, ints, picks: [chest]) : Save(world, name, ints, secondary: [chest]);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints)}]: {result}");

        return result;
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void TheCompareFieldsAreClampedOrResetInsteadOfRefused(string name)
    {
        Assert.Equal([0, 0, -20, 1], Saved(name, [0, 0, -20, 1]).Ints);
        Assert.Equal([1000000, 0, -20, 1], Saved(name, [1000000, 0, -20, 1]).Ints);

        foreach (var amount in new[] { 1000001, int.MaxValue }) {
            Assert.Equal([1000000, 0, -20, 1], Saved(name, [amount, 0, -20, 1]).Ints);
        }

        Assert.Equal([0, 0, -20, 1], Saved(name, [-1, 0, -20, 1]).Ints);
        Assert.Equal([0, 1, -20, 1], Saved(name, [0, 1, -20, 1]).Ints);
        Assert.Equal([0, 1, -20, 1], Saved(name, [0, 2, -20, 1]).Ints);
        Assert.Equal([0, 0, -20, 1], Saved(name, [0, -1, -20, 1]).Ints);

        foreach (var target in new[] { 0, 1, -10, -20 }) {
            Assert.Equal([0, 0, target, 1], Saved(name, [0, 0, target, 1]).Ints);
        }

        foreach (var target in new[] { 2, 5, -5 }) {
            Assert.Equal([0, 0, 0, 1], Saved(name, [0, 0, target, 1]).Ints);
        }

        foreach (var comparison in new[] { 0, 1, 2, 3, 4, 5 }) {
            Assert.Equal([0, 0, -20, comparison], Saved(name, [0, 0, -20, comparison]).Ints);
        }

        foreach (var comparison in new[] { 6, -1, 100 }) {
            Assert.Equal([0, 0, -20, 0], Saved(name, [0, 0, -20, comparison]).Ints);
        }
    }

    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void AMissingIntTakesTheFrameDefaultAndAnExtraIntIsDropped(string name)
    {
        Assert.Equal([3, 0, -20, 2], Saved(name, [3]).Ints);
        Assert.Equal([0, 0, -20, 2], Saved(name, []).Ints);
        Assert.Equal([0, 0, -20, 1], Saved(name, [0, 0, -20, 1, 9]).Ints);
    }

    [Fact]
    public void HasItemsAcceptsTheFurniChestAndTheCreditChest()
    {
        using var world = new G1World();

        Assert.True(Save(world, "wf_cnd_chest_has_items", picks: [world.AddChest(WiredChestKind.Furni).Id]).Accepted);
        Assert.True(Save(world, "wf_cnd_chest_has_items", picks: [world.AddChest(WiredChestKind.Coins).Id]).Accepted);
    }

    [Fact]
    public void HasItemTypeAcceptsOnlyTheFurniChestAndAnswersOtherPicksWithTheFurniChestKey()
    {
        using var world = new G1World();
        const string name = "wf_cnd_chest_has_item_type";
        var notAChest = world.AddItem("wf_colortile", Plus.HabboHotel.Items.InteractionType.None).Id;

        Assert.True(Save(world, name, secondary: [world.AddChest(WiredChestKind.Furni).Id]).Accepted);
        Assert.Equal("wiredfurni.error.require_furni_chests", Save(world, name, secondary: [world.AddChest(WiredChestKind.Coins).Id]).Key);
        Assert.Equal("wiredfurni.error.require_furni_chests", Save(world, name, secondary: [notAChest]).Key);
        Assert.Equal("wiredfurni.error.require_furni_chests",
            Save(world, name, secondary: [world.AddChest(WiredChestKind.Furni).Id, world.AddChest(WiredChestKind.Coins).Id]).Key);
    }

    [Fact]
    public void HasItemsRefusesAPickThatIsNotAChestWithTheChestKey()
    {
        using var world = new G1World();
        var notAChest = world.AddItem("wf_colortile", Plus.HabboHotel.Items.InteractionType.None).Id;

        Assert.Equal("wiredfurni.error.require_chests", Save(world, "wf_cnd_chest_has_items", picks: [notAChest]).Key);
        Assert.Equal("wiredfurni.error.require_chests",
            Save(world, "wf_cnd_chest_has_items", picks: [world.AddChest(WiredChestKind.Furni).Id, notAChest]).Key);
    }
}
