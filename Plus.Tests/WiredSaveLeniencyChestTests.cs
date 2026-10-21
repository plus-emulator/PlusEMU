using Xunit;

namespace Plus.Tests;

// The chest conditions have no text field: a text in the save is dropped. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredSaveLeniencyChestTests
{
    [Theory]
    [InlineData("wf_cnd_chest_has_items")]
    [InlineData("wf_cnd_chest_has_item_type")]
    public void AChestConditionDropsATextItDoesNotRead(string name)
    {
        using var save = new WiredRawSave(name);
        var chest = save.FurniChest();
        uint[] picks = name == "wf_cnd_chest_has_items" ? [chest] : [];
        uint[] secondary = name == "wf_cnd_chest_has_items" ? [] : [chest];
        var before = save.Save(picks: picks, secondary: secondary);
        Assert.True(before.Accepted, $"{name} plain save: {before}");

        var result = save.Save(ints: before.Ints, text: "abc", picks: picks, secondary: secondary);

        Assert.True(result.Accepted, $"{name} with a text: {result}");
        Assert.Equal("", result.Text);
        Assert.Equal(before.Ints, result.Ints);
    }
}
