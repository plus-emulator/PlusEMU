using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): wf_colortile is an ordinary 7-state furni with legacy stuff data. Wired
// toggle state cycles it 0..6, a user's use cycles it, and walking on it changes nothing.
public class ColourTileStateTests
{
    private static ItemDefinition ColourTile() => new()
    {
        Id = 29284,
        Type = ItemType.Floor,
        Width = 1,
        Length = 1,
        Height = 0.2,
        Stackable = true,
        Walkable = true,
        Modes = 7,
        ItemName = "wf_colortile",
        PublicName = "Color Tile",
        InteractionName = "default",
        InteractionType = InteractionTypes.GetTypeFromString("default"),
        AdjustableHeights = [],
        VendingIds = []
    };

    [Fact]
    public void FreshInstallAndMigrationSeedTheColourTileAsAnOrdinarySevenStateFurni()
    {
        var fresh = File.ReadAllLines(HabbiconPacketTests.Repo("Database/FreshInstall.sql"))
            .Single(line => line.StartsWith("(29284,'wf_colortile',", StringComparison.Ordinal));
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/85_ColourTileStates.sql"));

        Assert.Contains(",'default',0,7,", fresh);
        Assert.Contains("SET `interaction_type` = 'default', `interaction_modes_count` = 7", migration);
        Assert.Contains("WHERE `item_name` = 'wf_colortile'", migration);
        Assert.Contains("-- 85_ColourTileStates\n" + migration, File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql")));
    }

    [Fact]
    public void PlacingAnInventoryColourTileGivesItALegacyStateThatWiredCanCycle()
    {
        var inventory = new InventoryItem { Id = 5, OwnerId = 7, Definition = ColourTile(), ExtraData = FurniObjectData.Empty };

        var placed = inventory.ToRoomObject(new Habbo { Id = 7, Username = "johndoe" });

        var legacy = Assert.IsType<LegacyDataFormat>(placed.ExtraData);
        Assert.Equal("0", legacy.Data);
        Assert.Equal(InteractionType.None, placed.Definition.InteractionType);

        var seen = new List<string>();

        for (var tick = 0; tick < 7; tick++) {
            var next = ((int.TryParse(placed.LegacyDataString, out var state) ? state : 0) + 1) % placed.Definition.Modes;
            placed.LegacyDataString = next.ToString(System.Globalization.CultureInfo.InvariantCulture);
            seen.Add(placed.LegacyDataString);
        }

        Assert.Equal(["1", "2", "3", "4", "5", "6", "0"], seen);
    }

    [Fact]
    public void PlacingKeepsTypedInventoryData()
    {
        var data = new LegacyDataFormat { Data = "4" };
        var inventory = new InventoryItem { Id = 5, OwnerId = 7, Definition = ColourTile(), ExtraData = data };

        Assert.Same(data, inventory.ToRoomObject(new Habbo { Id = 7, Username = "johndoe" }).ExtraData);
    }
}
