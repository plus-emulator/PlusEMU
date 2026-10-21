using Xunit;

namespace Plus.Tests;

public sealed class EffectBoxMigrationTests
{
    [Fact]
    public void TheMigrationGivesDefaultEffectBoxesTheEffectBoxInteraction()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/107_EffectBoxInteractions.sql"));

        Assert.Contains("UPDATE furniture SET interaction_type = 'effect_box'", sql);
        Assert.Contains("item_name LIKE 'fxbox\\_fx%'", sql);
        Assert.Contains("interaction_type IN ('default', 'default_floor', 'effect_box')", sql);
        Assert.DoesNotContain("TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheFreshInstallGivesEveryEffectBoxTheEffectBoxInteraction()
    {
        var rows = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")).Split('\n')
            .Where(line => line.StartsWith('(') && line.Contains(",'fxbox_fx") && line.Contains(",'s',")).ToList();

        Assert.Equal(18, rows.Count);
        Assert.All(rows, row => Assert.Contains(",'effect_box',0,2,", row));
    }
}
