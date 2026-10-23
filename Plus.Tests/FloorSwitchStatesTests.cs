using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (3 of 3 runs): clicks and Toggle to Random turn a wf_floor_switch1 between 0 and 1 only.
public sealed class FloorSwitchStatesTests
{
    [Fact]
    public void TheMigrationGivesTheFloorSwitchTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/117_FloorSwitchTwoStates.sql"));

        Assert.Contains("SET interaction_modes_count = 2", sql);
        Assert.Contains("item_name = 'wf_floor_switch1'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesTheFloorSwitchTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains(",'wf_floor_switch1','Floor Switch 1',"));

        Assert.Contains(",'wf_floor_switch1',0,2,'0','0',", row);
    }
}
