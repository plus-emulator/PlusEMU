using System.Text.RegularExpressions;
using Xunit;

namespace Plus.Tests;

// The official achievement set as migration 76 and the fresh install ship it (habbo.com public achievements API, 2026-10-11).
public class AchievementSeedTests
{
    private static readonly string[] OfficialCategories = ["identity", "explore", "social", "pets", "games", "room_builder", "crackables", "crafting", "collectibles"];

    private static readonly Regex MigrationRow = new(
        @"^\('(?<group>ACH_\w+)','(?<category>\w+)',(?<level>\d+),(?<needed>\d+),(?<official>\d+),(?<state>\d+),'(?<badge>ACH_\w+)'\)[,;]$",
        RegexOptions.Multiline);

    private static readonly Regex FreshRow = new(
        @"^\((?<id>\d+),'(?<group>[^']+)','(?<category>[^']*)',(?<level>\d+),(?<pixels>-?\d+),(?<points>-?\d+),(?<needed>-?\d+),(?<game>\d+)," +
        @"(?<official>NULL|\d+),(?<state>\d+),(?<visible>[01]),(?:NULL|'(?<badge>[^']*)')\)[,;]$",
        RegexOptions.Multiline);

    [Fact]
    public void MigrationListsTheOfficialHabboComSet()
    {
        var rows = Official();
        var groups = rows.GroupBy(row => row.Group).ToList();

        Assert.Equal(1876, rows.Count);
        Assert.Equal(166, groups.Count);
        Assert.Equal(OfficialCategories.Order(), rows.Select(row => row.Category).Distinct().Order());
        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(row => row.State).Distinct().Order());
        Assert.Equal(166, rows.Select(row => row.OfficialId).Distinct().Count());

        foreach (var group in groups) {
            Assert.Equal(Enumerable.Range(1, group.Count()), group.Select(row => row.Level));
            Assert.Single(group.Select(row => (row.Category, row.OfficialId, row.State)).Distinct());
        }

        // ACH_<code><level>, except the one-level ACH_Profile1 and ACH_Profile2; badge_definitions.code holds 35 characters.
        Assert.Equal(new[] { "ACH_Profile1", "ACH_Profile2" }, rows.Where(row => row.Badge != row.Group + row.Level).Select(row => row.Badge));
        Assert.All(rows, row => Assert.True(row.Badge.Length <= 35, row.Badge));
        // AIR orders categories by first appearance and appends archive; in habbo.com order that is the official order.
        Assert.Equal(OfficialCategories, rows.Where(row => row.State != 2).OrderBy(row => row.OfficialId).Select(row => row.Category).Distinct());
    }

    [Fact]
    public void OriginalDatabaseCarriesTheMigration()
    {
        var migration = Read("Database/Migrations/76_OfficialAchievements.sql");

        Assert.Contains("-- 76_OfficialAchievements\n" + migration, Read("Resources/SQLs/Original Database.sql"));
    }

    [Fact]
    public void FreshInstallListsExactlyTheOfficialSetAndDefinesEveryBadge()
    {
        var fresh = Read("Database/FreshInstall.sql");
        var start = fresh.IndexOf("INSERT INTO `achievements` VALUES", StringComparison.Ordinal);
        var achievements = fresh[start..fresh.IndexOf(";\n", start, StringComparison.Ordinal)] + ";";
        var rows = FreshRow.Matches(achievements).Select(match => (
            Group: match.Groups["group"].Value, Category: match.Groups["category"].Value, Level: int.Parse(match.Groups["level"].Value),
            Needed: int.Parse(match.Groups["needed"].Value), Official: match.Groups["official"].Value, State: int.Parse(match.Groups["state"].Value),
            Visible: match.Groups["visible"].Value == "1", Badge: match.Groups["badge"].Success ? match.Groups["badge"].Value : null)).ToList();
        var definitions = BadgeDefinitions(fresh);

        Assert.Equal(achievements.Count(character => character == '\n'), rows.Count);
        Assert.Equal(Official().Select(row => (row.Group, row.Category, row.Level, row.Needed, row.OfficialId.ToString(), row.State)).Order(),
            rows.Where(row => row.Visible).Select(row => (row.Group, row.Category, row.Level, row.Needed, row.Official, row.State)).Order());
        // The hotel's own achievements stay for their owners, out of the list.
        Assert.Equal(new[] { "ACH_BaseJumpBigParachute", "ACH_BaseJumpDaysPlayed", "ACH_BaseJumpMissile", "ACH_BaseJumpShield", "ACH_BaseJumpWins",
            "ACH_HabboWayGraduate", "ACH_Motto", "ACH_SelfModBanSeen", "ACH_SelfModChatFloodFilterSeen", "ACH_SelfModChatHearRangeSeen",
            "ACH_SelfModChatScrollSpeedSeen", "ACH_SelfModIgnoreSeen", "ACH_SelfModKickSeen", "ACH_SelfModMuteSeen", "ACH_SelfModRoomFilterSeen",
            "ACH_SelfModWalkthroughSeen" }, rows.Where(row => !row.Visible).Select(row => row.Group).Distinct().Order());
        Assert.All(rows.Where(row => !row.Visible), row => Assert.Equal("NULL", row.Official));
        Assert.All(rows, row => Assert.Contains(row.Badge ?? row.Group + row.Level, definitions));
    }

    internal static List<(string Group, string Category, int Level, int Needed, int OfficialId, int State, string Badge)> Official() =>
        MigrationRow.Matches(Read("Database/Migrations/76_OfficialAchievements.sql")).Select(match => (
            match.Groups["group"].Value, match.Groups["category"].Value, int.Parse(match.Groups["level"].Value), int.Parse(match.Groups["needed"].Value),
            int.Parse(match.Groups["official"].Value), int.Parse(match.Groups["state"].Value), match.Groups["badge"].Value)).ToList();

    private static HashSet<string> BadgeDefinitions(string dump)
    {
        var start = dump.IndexOf("INSERT INTO `badge_definitions` VALUES", StringComparison.Ordinal);
        var block = dump[start..dump.IndexOf(";\n", start, StringComparison.Ordinal)];

        return Regex.Matches(block, @"^\('([^']+)','[^']*'\)", RegexOptions.Multiline).Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string Read(string path) => File.ReadAllText(HabbiconPacketTests.Repo(path));
}
