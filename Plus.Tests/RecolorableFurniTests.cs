using System.Text.RegularExpressions;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): the *_recolorable Builders Club furni carry MapData r, g, b and state; a
// fresh one is {"r":"255","b":"255","g":"255","state":"0"} and its state cycles like any multi-state furni.
public class RecolorableFurniTests
{
    internal static ItemDefinition Block(int modes = 5) => new()
    {
        Id = 19348,
        SpriteId = 19348,
        Type = ItemType.Floor,
        Width = 1,
        Length = 1,
        Height = 1,
        Stackable = true,
        Walkable = true,
        Modes = modes,
        ItemName = "bc_block_0_recolorable",
        PublicName = "Small Block",
        InteractionName = "recolorable",
        InteractionType = InteractionTypes.GetTypeFromString("recolorable"),
        AdjustableHeights = [],
        VendingIds = []
    };

    private static List<KeyValuePair<string, string>> WireMap(IFurniObjectData data) =>
        [.. Assert.IsType<FurnitureDataSnapshot.Map>(FurnitureDataSnapshot.Capture(data)).Values];

    [Fact]
    public void AFreshRecolorableIsWhiteInStateZeroAndGoesOutAsHabbosMap()
    {
        var data = FurniExtraData.Load(Block(), "", keepLegacy: false);

        var recolorable = Assert.IsType<RecolorableDataFormat>(data);
        Assert.Equal(InteractionType.Recolorable, Block().InteractionType);
        Assert.Equal(FurniDataStructure.Map, data.StructureType);
        Assert.Equal((0xFFFFFF, "0"), (recolorable.Rgb, recolorable.Data));
        Assert.Equal([new("r", "255"), new("b", "255"), new("g", "255"), new("state", "0")], WireMap(data));
    }

    [Fact]
    public void StoredColourAndStateSurviveALoadAndThePlainStateStaysLegacy()
    {
        var data = Assert.IsType<RecolorableDataFormat>(FurniExtraData.Load(Block(), "", keepLegacy: true));
        Assert.True(data.SetColor(0x12AB7F));
        data.Data = "3";

        var stored = data.Serialize();
        var loaded = Assert.IsType<RecolorableDataFormat>(FurniExtraData.Load(Block(), stored, keepLegacy: false));

        Assert.Equal("state\t3\nr\t18\ng\t171\nb\t127", stored);
        Assert.Equal((0x12AB7F, 18, 171, 127, "3"), (loaded.Rgb, loaded.Red, loaded.Green, loaded.Blue, loaded.Data));
        Assert.Equal([new("r", "18"), new("b", "127"), new("g", "171"), new("state", "3")], WireMap(loaded));
    }

    [Theory]
    [InlineData("2", 0xFFFFFF, "2")]
    [InlineData("0,128,255", 0x0080FF, "0")]
    [InlineData("0,128,256", 0xFFFFFF, "0")]
    [InlineData("state\t1\nr\t999\ng\tx\nb\t7", 0xFFFF07, "1")]
    [InlineData("garbage", 0xFFFFFF, "0")]
    public void ReadsLegacyStatesCatalogueColoursAndClampsChannelsLikeTheClient(string stored, int rgb, string state)
    {
        var data = Assert.IsType<RecolorableDataFormat>(FurniExtraData.Load(Block(), stored, keepLegacy: true));

        Assert.Equal((rgb, state), (data.Rgb, data.Data));
    }

    [Fact]
    public void ColourWritesOutsideTheRgbRangeOrUnchangedAreRefused()
    {
        var data = new RecolorableDataFormat();

        Assert.False(data.SetColor(-1));
        Assert.False(data.SetColor(0x1000000));
        Assert.False(data.SetColor(0xFFFFFF));
        Assert.True(data.SetColor(0));
        Assert.Equal(0, data.Rgb);
    }

    [Fact]
    public void ItemStateUseAndPlacementKeepTheColour()
    {
        var inventory = new InventoryItem { Id = 5, OwnerId = 7, Definition = Block(), ExtraData = FurniExtraData.Load(Block(), "0,0,255", keepLegacy: false) };
        var placed = inventory.ToRoomObject(new Habbo { Id = 7, Username = "johndoe" });

        Assert.Same(inventory.ExtraData, placed.ExtraData);
        placed.LegacyDataString = "4";

        var data = Assert.IsType<RecolorableDataFormat>(placed.ExtraData);
        Assert.Equal(("4", 0x0000FF), (placed.LegacyDataString, data.Rgb));
        Assert.Equal("state\t4\nr\t0\ng\t0\nb\t255", placed.ToInventoryItem().ExtraData.Serialize());
    }

    [Fact]
    public void DataUpdatesCarryTheMapForRecolorablesAndLegacyForOtherFurni()
    {
        var recolorable = new RecolorableDataFormat();
        recolorable.SetColor(0x010203);

        var map = Assert.IsType<FurnitureDataSnapshot.Map>(FurnitureWireData.ForState(recolorable, "2"));
        Assert.Equal([new("r", "1"), new("b", "3"), new("g", "2"), new("state", "2")], [.. map.Values]);
        Assert.Equal(new FurnitureDataSnapshot.Legacy("2"), FurnitureWireData.ForState(new LegacyDataFormat { Data = "1" }, "2"));

        var packet = new Packet();
        new ObjectsDataUpdateComposer([(9u, FurnitureWireData.ForState(recolorable, "2"))]).Compose(packet);
        var input = new FlashIncomingPacket { Buffer = packet.Bytes() };

        Assert.Equal((1, 9, 1, 4), (input.ReadInt(), input.ReadInt(), input.ReadInt(), input.ReadInt()));
        Assert.Equal(["r", "1", "b", "3", "g", "2", "state", "2"], Enumerable.Range(0, 8).Select(_ => input.ReadString()).ToArray());
    }

    [Fact]
    public void MigrationSeedsAllSixtyNineClassesAndTheFreshInstallAndOriginalDatabaseCarryIt()
    {
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/87_RecolorableBuildersClubFurni.sql"));
        var rows = Regex.Matches(migration, @"^    \('(bc_[a-z0-9_]+_recolorable)', '[^']+', (\d+),", RegexOptions.Multiline);

        Assert.Equal(69, rows.Count);
        Assert.Equal(69, rows.Select(row => row.Groups[1].Value).Distinct().Count());
        Assert.Equal(Enumerable.Range(19320, 69), rows.Select(row => int.Parse(row.Groups[2].Value)).Order());
        Assert.Contains("    ('bc_block_0_recolorable', 'Small Block', 19348, 1, 1, 1, 1, 1, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_recolorable', 0, 5, 35202, NULL, 'bc_blocks_recolorable', 0, NULL)", migration);
        Assert.Contains("    ('bc_alpha1_alpha_recolorable', 'Alphabet Block', 19321, 1, 1, 1, 1, 0, 0, 0, 0, 75914, 1, 1, 'other', 'buildersclub_alpha1_recolorable', 0, 26, 35175, NULL, 'bc_alphabet_recolorable', 1, NULL)", migration);
        Assert.Contains("-- 87_RecolorableBuildersClubFurni\n" + migration, File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql")));

        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        Assert.Matches(@"\(\d+,'bc_block_0_recolorable','Small Block','s',1,1,1,1,0,1,19348,1,0,0,0,0,'recolorable',0,5,", fresh);
        Assert.Contains("'bc_blocks_recolorable','recolorable_furniture',1,1,0)", fresh);
        Assert.Contains("'bc_alphabet_recolorable','recolorable_furniture',1,1,0)", fresh);
        Assert.DoesNotContain("_recolorable','default_3x3'", fresh);
        Assert.Contains("(35202,'bc_block_0_recolorable',1,0,0,0,1,1,'')", fresh);
        Assert.Equal(69, Regex.Matches(fresh, @"^\(\d+,'bc_[a-z0-9_]+_recolorable','[^']+','s',(?:[^,]+,){11}0,'recolorable',", RegexOptions.Multiline).Count);
    }

    // Habbo's furnidata (r75) gives ten of the classes customparams; the rest have none.
    [Fact]
    public void MigrationAndFreshInstallCarryHabbosCustomParamsAndHabbosPageLayout()
    {
        var expected = new Dictionary<string, string>
        {
            ["bc_block_0_recolorable"] = "-0.25",
            ["bc_block_1_recolorable"] = "-0.25",
            ["bc_block_small_recolorable"] = "-0.25",
            ["bc_standingtriangularprism_recolorable"] = "-0.25",
            ["bc_cylinder_recolorable"] = "-0.25",
            ["bc_round_recolorable"] = "-0.25",
            ["bc_standinghalfcylinder_recolorable"] = "-0.25",
            ["bc_panel_recolorable"] = "-0.5",
            ["bc_stick_recolorable"] = "-1.00",
            ["bc_block_water_recolorable"] = "30"
        };
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/87_RecolorableBuildersClubFurni.sql"));
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));

        foreach (var (name, value) in expected) {
            Assert.Contains($"        WHEN '{name}' THEN '{value}'", migration);
        }

        Assert.Equal(expected.Count, Regex.Matches(migration, @"^        WHEN 'bc_[a-z0-9_]+_recolorable' THEN '", RegexOptions.Multiline).Count);
        Assert.Contains("f.allow_inventory_stack = 0", migration);
        Assert.Equal(2, Regex.Matches(migration, @"'bc_(?:blocks|alphabet)_recolorable', 'recolorable_furniture', 1, 1, 0 FROM catalog_pages p").Count);

        foreach (var line in fresh.Split('\n').Where(line => line.StartsWith("(1") && line.Contains("_recolorable','") && line.Contains(",'recolorable',"))) {
            var name = Regex.Match(line, @"^\(\d+,'([^']+)'").Groups[1].Value;
            var customParams = expected.TryGetValue(name, out var value) ? $"'{value}'" : "NULL";
            Assert.Contains($"desc',NULL,0,{customParams},", line);
        }
    }

    [Theory]
    [InlineData("bc_block_0_recolorable", 69, 0xFFFFFF)]
    [InlineData("bc_alpha1_z_recolorable", 69, 0xFFFFFF)]
    [InlineData("bc_block_artdeco1_recolorable", 5, 0x672913)]
    [InlineData("bc_block_glass_recolorable", 14, 0xFFFFFF)]
    [InlineData("bc_block_grass_recolorable", 7, 0xB0B1AE)]
    [InlineData("bc_block_water_recolorable", 6, 0x336666)]
    [InlineData("bc_wood_recolorable", 6, 0x636568)]
    [InlineData("not_a_recolorable", 69, 0xFFFFFF)]
    public void PaletteFollowsHabbosPaletteToFurniWithTheDefaultFallback(string className, int count, int first)
    {
        var colours = RecolorablePalette.GetColors(className);

        Assert.Equal((count, first), (colours.Count, colours[0]));
        Assert.Equal(first, RecolorablePalette.PurchaseColor(className, null));
        Assert.Equal(first, RecolorablePalette.PurchaseColor(className, 0x123456));
        Assert.Equal(colours[^1], RecolorablePalette.PurchaseColor(className, colours[^1]));
    }

    [Fact]
    public void EveryMigratedClassHasItsOwnPaletteEntryAndCatalogueColoursRoundTrip()
    {
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/87_RecolorableBuildersClubFurni.sql"));

        foreach (Match row in Regex.Matches(migration, @"^    \('(bc_[a-z0-9_]+_recolorable)'", RegexOptions.Multiline)) {
            Assert.NotEmpty(RecolorablePalette.GetColors(row.Groups[1].Value));
        }

        Assert.Equal("0,133,255", RecolorableDataFormat.FormatCatalogueColor(0x0085FF));
        Assert.True(RecolorableDataFormat.TryParseCatalogueColor(RecolorableDataFormat.FormatCatalogueColor(0xB0B1AE), out var rgb));
        Assert.Equal(0xB0B1AE, rgb);
    }

    private sealed class Packet : IOutgoingPacket
    {
        private readonly MemoryStream _stream = new();
        public int MessageId { get; set; }
        public ReadOnlyMemory<byte> Buffer => _stream.ToArray();
        public byte[] Bytes() => _stream.ToArray();
        public void WriteByte(byte value) => _stream.WriteByte(value);
        public void WriteShort(short value) => Write([(byte)(value >> 8), (byte)value]);
        public void WriteInt(int value) => Write([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);
        public void WriteInteger(int value) => WriteInt(value);
        public void WriteUInt(uint value) => WriteInt(unchecked((int)value));
        public void WriteUInteger(uint value) => WriteInt(unchecked((int)value));
        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);
        public void WriteBoolean(bool value) => WriteBool(value);
        public void WriteDouble(double value) => WriteString(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        public void WriteString(string value)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? "");
            WriteShort((short)bytes.Length);
            Write(bytes);
        }

        private void Write(byte[] bytes) => _stream.Write(bytes);
    }
}
