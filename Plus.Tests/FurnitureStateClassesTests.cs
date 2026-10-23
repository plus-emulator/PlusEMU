using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (a floor item read from the room after it was placed from Builders Club, and 3 runs of user
// double-clicks on 34 classes): the two plain game tiles, the pyramid, the blobs, the box, the buttons, the arrow, pressure and ring plates have usage
// policy 0 and ignore a double-click; the other state furni can be used by controllers and the floor switches by anyone. New blobs start in
// state 1 and new buttons, game tiles and boxes have no state; the glass door and the fire gates start in state 0.
public sealed class FurnitureStateClassesTests
{
    private static Item Placed(string name, int modes, string interaction = "default", string stored = "")
    {
        var definition = new ItemDefinition
        {
            Type = ItemType.Floor,
            ItemName = name,
            PublicName = name,
            InteractionName = interaction,
            InteractionType = InteractionTypes.GetTypeFromString(interaction),
            Modes = modes,
            Width = 1,
            Length = 1,
            VendingIds = [],
            AdjustableHeights = []
        };

        return new() { Id = 1, Definition = definition, ExtraData = FurniExtraData.Load(definition, stored, keepLegacy: true) };
    }

    [Theory]
    [InlineData("wf_tile1", 10, "default", 0)]
    [InlineData("wf_tile2", 10, "default", 0)]
    [InlineData("wf_pyramid", 4, "gate", 0)]
    [InlineData("wf_blob", 2, "default", 0)]
    [InlineData("wf_blob2", 2, "default", 0)]
    [InlineData("wf_box", 1, "default", 0)]
    [InlineData("wf_button", 2, "default", 0)]
    [InlineData("wf_button_red", 2, "default", 0)]
    [InlineData("wf_arrowplate", 4, "pressure_tile", 0)]
    [InlineData("wf_arrowplate_8dir", 4, "pressure_pad", 0)]
    [InlineData("wf_pressureplate", 4, "pressure_tile", 0)]
    [InlineData("wf_ringplate", 4, "pressure_tile", 0)]
    [InlineData("wf_colortile", 7, "default", 1)]
    [InlineData("wf_numbertile1", 10, "default", 1)]
    [InlineData("wf_glowball", 2, "default", 1)]
    [InlineData("wf_antenna1", 4, "antenna", 1)]
    [InlineData("wf_glassdoor", 2, "gate", 1)]
    [InlineData("wf_floor_switch1", 2, "wf_floor_switch1", 2)]
    public void TheseClassesCarryHabbosUsagePolicy(string name, int modes, string interaction, int usage) =>
        Assert.Equal(usage, RoomItemSnapshot.Capture(Placed(name, modes, interaction)).UseButton);

    [Theory]
    [InlineData("wf_tile1", 10, "default", "")]
    [InlineData("wf_tile2", 10, "default", "")]
    [InlineData("wf_box", 1, "default", "")]
    [InlineData("wf_button", 2, "default", "")]
    [InlineData("wf_button_red", 2, "default", "")]
    [InlineData("wf_button_x", 2, "default", "")]
    [InlineData("wf_blob", 2, "default", "1")]
    [InlineData("wf_blob2", 2, "default", "1")]
    [InlineData("wf_glassdoor", 2, "gate", "0")]
    [InlineData("wf_firegate", 2, "gate", "0")]
    [InlineData("wf_arrowplate_8dir", 4, "pressure_pad", "0")]
    [InlineData("wf_colortile", 7, "default", "0")]
    [InlineData("wf_numbertile1", 10, "default", "0")]
    public void ANewItemOfTheseClassesStartsInHabbosState(string name, int modes, string interaction, string state) =>
        Assert.Equal(state, Placed(name, modes, interaction).LegacyDataString);

    [Fact]
    public void AStoredStateIsKept() => Assert.Equal("3", Placed("wf_blob", 2, "default", "3").LegacyDataString);

    [Fact]
    public void TheMigrationGivesTheBlobsTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/118_BlobTwoStates.sql"));

        Assert.Contains("SET interaction_modes_count = 2", sql);
        Assert.Contains("'wf_blob', 'wf_blob2'", sql);
    }

    [Theory]
    [InlineData("wf_blob", "WIRED GAME: Blob")]
    [InlineData("wf_blob2", "WIRED GAME: Super Blob")]
    public void TheFreshInstallGivesTheBlobsTwoStates(string name, string publicName)
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains($",'{name}','{publicName}',"));

        Assert.Contains(",'default',0,2,'0','0',", row);
    }
}
