using System.Text.RegularExpressions;
using Xunit;

namespace Plus.Tests;

public sealed class RecyclerMigrationTests
{
    [Fact]
    public void RebuildingTheFreshInstallKeepsEveryExistingContentTable()
    {
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var script = File.ReadAllText(HabbiconPacketTests.Repo("scripts/build-fresh-install-sql.sh"));
        var tables = Regex.Matches(fresh, "INSERT INTO `([^`]+)`").Select(match => match.Groups[1].Value).Distinct().ToArray();
        Assert.NotEmpty(tables);
        var content = Regex.Match(script, "CONTENT_TABLES=\"([^\"]+)\"").Groups[1].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        Assert.All(tables, table => Assert.Contains(table, content));
    }
}
