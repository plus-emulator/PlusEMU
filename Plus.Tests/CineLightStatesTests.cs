using Xunit;

namespace Plus.Tests;

// Clicks turn a cine_light1 from 0 to 1, to 2 and back to 0.
public sealed class CineLightStatesTests
{
    [Fact]
    public void TheMigrationGivesTheTheaterLightThreeStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/116_CineLightThreeStates.sql"));

        Assert.Contains("SET interaction_modes_count = 3", sql);
        Assert.Contains("item_name = 'cine_light1'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesTheTheaterLightThreeStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains(",'cine_light1','Theater Lights',"));

        Assert.Contains(",'default',0,3,'0','0',", row);
    }
}
