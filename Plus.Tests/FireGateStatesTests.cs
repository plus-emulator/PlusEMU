using Xunit;

namespace Plus.Tests;

// Toggle State turns the fire gate from 0 to 1 and back to 0.
public sealed class FireGateStatesTests
{
    [Fact]
    public void TheMigrationGivesTheFireGateTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/112_FireGateTwoStates.sql"));

        Assert.Contains("SET interaction_modes_count = 2", sql);
        Assert.Contains("item_name = 'wf_firegate'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesTheFireGateTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains(",'wf_firegate','Wall Of Flame',"));

        Assert.Contains(",'gate',0,2,'0','0',", row);
    }
}
