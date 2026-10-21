using Xunit;

namespace Plus.Tests;

public sealed class GlowballStatesTests
{
    [Fact]
    public void TheMigrationGivesTheGlowballTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/110_GlowballTwoStates.sql"));

        Assert.Contains("SET interaction_modes_count = 2", sql);
        Assert.Contains("item_name = 'wf_glowball'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesTheGlowballTwoStates()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var row = sql.Split('\n').Single(line => line.StartsWith('(') && line.Contains(",'wf_glowball','Glowball',"));

        Assert.Contains(",'default',0,2,'0','0',", row);
    }
}
