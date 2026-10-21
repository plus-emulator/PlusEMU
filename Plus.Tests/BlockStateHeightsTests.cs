using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: the small Builders Club block is 1.0, 0.75, 0.5, 0.25 and 0.0 high in states 0 to 4.
public sealed class BlockStateHeightsTests
{
    private const string Heights = "1.0,0.75,0.5,0.25,0.0";

    [Fact]
    public void TheMigrationGivesTheSmallBlockItsStateHeights()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/113_BlockStateHeights.sql"));

        Assert.Contains($"SET height_adjustable = '{Heights}'", sql);
        Assert.Contains("item_name = 'bc_block_0_recolorable'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesTheSmallBlockItsStateHeights()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains(",'bc_block_0_recolorable','Small Block',"));

        Assert.Contains($",'recolorable',0,5,'0','{Heights}',", row);
    }
}
