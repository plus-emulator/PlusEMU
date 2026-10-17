using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): every firing flips the trigger box's legacy state "1"/"0" in the flash
// ObjectsDataUpdate (says something, periodically, long periodically, walks on). Boxes loaded from the inventory kept empty
// stuff data, so their flashes could not change it and they went out as "" on every firing.
public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData("wf_trg_says_something", false)]
    [InlineData("wf_act_toggle_state", false)]
    [InlineData("wf_slc_furni_area", false)]
    [InlineData("wf_trg_periodically", true)]
    public void WiredBoxesKeepLegacyDataThatRestsAtZero(string name, bool keepLegacy)
    {
        var definition = BoxDefinition(name);

        var fresh = Assert.IsType<LegacyDataFormat>(FurniExtraData.Load(definition, "", keepLegacy));
        var flashed = Assert.IsType<LegacyDataFormat>(FurniExtraData.Load(definition, "1", keepLegacy));

        Assert.Equal("0", fresh.Data);
        Assert.Equal("1", flashed.Data);
    }

    [Fact]
    public void OrdinaryFurniInTheInventoryStillLoadsWithoutLegacyData()
    {
        var definition = BoxDefinition("plain_chair");

        Assert.IsNotType<LegacyDataFormat>(FurniExtraData.Load(definition, "", keepLegacy: false));
    }

    [Fact]
    public void PeriodicTriggerFromTheInventoryFlashesOnEveryFiring()
    {
        var (f, _) = TimerGridRoom("wf_trg_periodically");
        var trigger = f.Items[41];
        trigger.ExtraData = FurniExtraData.Load(trigger.Definition, "", keepLegacy: false);
        Assert.Equal("0", trigger.LegacyDataString);
        var states = new List<string>();
        var last = trigger.LegacyDataString;

        // The flash goes out with the effect, one wired tick after each firing (AIR WIN63-202610151745-318700621).
        for (var now = 50; now <= 2050; now += 50) {
            f.Advance(50);

            if (trigger.LegacyDataString != last) {
                last = trigger.LegacyDataString;
                states.Add(last);
            }
        }

        Assert.Equal(["1", "0", "1", "0"], states);
    }

    [Fact]
    public void SaysTriggerFromTheInventoryFlashesOnEveryFiring()
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [1, 0], [ObservedSubjectId], 100))
        };
        var f = new Fixture(layout, live: true);
        var trigger = f.Items[ObservedTriggerId];
        trigger.ExtraData = FurniExtraData.Load(trigger.Definition, "", keepLegacy: false);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var states = new List<string>();

        for (var i = 0; i < 3; i++) {
            f.Owned(() => rider.OnChat(0, "alpha", false));
            f.Advance(500);
            states.Add(trigger.LegacyDataString);
        }

        Assert.Equal(["1", "0", "1"], states);
    }

    private static ItemDefinition BoxDefinition(string name) => new()
    {
        Type = ItemType.Floor,
        Width = 1,
        Length = 1,
        Modes = 1,
        ItemName = name,
        PublicName = name,
        InteractionName = name,
        InteractionType = InteractionTypes.GetTypeFromString(name),
        AdjustableHeights = [],
        VendingIds = []
    };
}

public class WiredDefinitionFlashStateTests
{
    // Observed on AIR WIN63-202610151745-318700621 and r90: give 6 then 7, change 2, remove 8 on a variable definition box.
    [Fact]
    public void ADefinitionShowsTheKindOfItsLastWriteAndAlternatesWithinIt()
    {
        Assert.Equal("6", Plus.HabboHotel.Items.Wired.WiredFlashStates.NextDefinition("0", Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionGiven));
        Assert.Equal("7", Plus.HabboHotel.Items.Wired.WiredFlashStates.NextDefinition("6", Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionGiven));
        Assert.Equal("6", Plus.HabboHotel.Items.Wired.WiredFlashStates.NextDefinition("7", Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionGiven));
        Assert.Equal("2", Plus.HabboHotel.Items.Wired.WiredFlashStates.NextDefinition("7", Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionChanged));
        Assert.Equal("8", Plus.HabboHotel.Items.Wired.WiredFlashStates.NextDefinition("2", Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionRemoved));
    }
}
