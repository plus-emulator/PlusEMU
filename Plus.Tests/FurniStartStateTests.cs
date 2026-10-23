using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// A new Wired counter, floor switch, antenna or colour tile has state "0", so a Move/Rotate update carries "0" and starting a counter at 0 sends nothing until it shows 1.
public sealed class FurniStartStateTests
{
    [Theory]
    [InlineData(InteractionType.Counter, "wf_game_upcounter1", "wf_game_upcounter1", 2)]
    [InlineData(InteractionType.Counter, "wf_upcounter1", "wf_upcounter1", 1)]
    [InlineData(InteractionType.WfFloorSwitch1, "wf_floor_switch1", "wf_floor_switch1", 3)]
    [InlineData(InteractionType.None, "wf_antenna2", "antenna", 4)]
    [InlineData(InteractionType.None, "wf_colortile", "default", 7)]
    public void AnEmptyStateOfASwitchOrWiredCounterStartsAtZero(InteractionType interaction, string name, string interactionName, int modes)
    {
        var definition = Definition(interaction, name, interactionName, modes);

        Assert.Equal("0", FurniExtraData.Load(definition, "", keepLegacy: true).Serialize());
        Assert.Equal("3", FurniExtraData.Load(definition, "3", keepLegacy: true).Serialize());
    }

    [Theory]
    [InlineData(InteractionType.None, "chair", "default", 1)]
    [InlineData(InteractionType.FreezeTileBlock, "es_box", "freeze_block", 2)]
    [InlineData(InteractionType.Counter, "game_timer", "counter", 2)]
    [InlineData(InteractionType.Scoreboard, "scoreboard", "scoreboard", 100)]
    public void FurniThatUseAnEmptyStateKeepIt(InteractionType interaction, string name, string interactionName, int modes)
    {
        Assert.Equal("", FurniExtraData.Load(Definition(interaction, name, interactionName, modes), "", keepLegacy: true).Serialize());
    }

    private static ItemDefinition Definition(InteractionType interaction, string name, string interactionName, int modes) => new()
    {
        Type = ItemType.Floor,
        ItemName = name,
        InteractionName = interactionName,
        InteractionType = interaction,
        Modes = modes,
        VendingIds = [],
        AdjustableHeights = []
    };
}
