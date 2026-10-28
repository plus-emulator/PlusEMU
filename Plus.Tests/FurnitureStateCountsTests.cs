using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Plus.Database.Migrator;
using Xunit;

namespace Plus.Tests;

public sealed partial class FurnitureStateCountsTests
{
    private static readonly string[] MigrationFiles =
    [
        "Database/Migrations/127_FurnitureStateCounts.sql",
        "Database/Migrations/128_MoreFurnitureStateCounts.sql",
    ];

    [GeneratedRegex(@"^UPDATE furniture SET interaction_modes_count = (\d+) WHERE .* IN \((.*)\);$", RegexOptions.Multiline)]
    private static partial Regex CountUpdate();

    [GeneratedRegex(@"'((?:[^'\\]|''|\\.)*)'")]
    private static partial Regex Quoted();

    private static (string Name, int Count)[] FixedClasses(string migrationFile) =>
    [
        .. CountUpdate().Matches(File.ReadAllText(HabbiconPacketTests.Repo(migrationFile))).SelectMany(match =>
            Quoted().Matches(match.Groups[2].Value).Select(name => (name.Groups[1].Value.Replace("''", "'"), int.Parse(match.Groups[1].Value))))
    ];

    // The count a hotel ends with: a later migration wins over an earlier one.
    private static (string Name, int Count)[] FixedClasses()
    {
        var counts = new Dictionary<string, int>();

        foreach (var file in MigrationFiles) {
            foreach (var (name, count) in FixedClasses(file)) {
                counts[name] = count;
            }
        }

        return [.. counts.Select(entry => (entry.Key, entry.Value))];
    }

    [Fact]
    public void EachMigrationNamesEachClassOnce()
    {
        foreach (var file in MigrationFiles) {
            var fixes = FixedClasses(file);

            Assert.NotEmpty(fixes);
            Assert.Equal(fixes.Length, fixes.Select(fix => fix.Name).Distinct().Count());
        }
    }

    [Fact]
    public void TheMigrationsFixTheWiredFurniOfThisChange()
    {
        var fixes = FixedClasses();

        Assert.Contains(("wf_antenna1", 2), fixes);
        Assert.Contains(("wf_antenna2", 2), fixes);
        Assert.Contains(("wf_fx_firegate", 2), fixes);
        Assert.Contains(("wf_teamcolortile", 10), fixes);
    }

    [Theory]
    [InlineData("hween_c18_labtilefloor", 3)]
    [InlineData("hygge_c25_carpetfloor", 3)]
    [InlineData("vwave_c21_trash", 4)]
    [InlineData("hween_c26_circle", 12)]
    [InlineData("cine_curtain", 2)]
    [InlineData("s12_bonfire", 2)]
    public void TheSecondMigrationGivesAClassTheCountHabboCyclesThrough(string name, int count)
    {
        Assert.Contains((name, count), FixedClasses(MigrationFiles[1]));
    }

    [Fact]
    public void TheFreshInstallHoldsTheMigratedCountOfEveryFixedClass()
    {
        var fixes = FixedClasses().ToDictionary(fix => fix.Name, fix => fix.Count);
        var sql = File.ReadAllLines(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var rows = sql.SkipWhile(line => !line.StartsWith("INSERT INTO `furniture`", StringComparison.Ordinal)).Where(line => line.StartsWith('(')).ToArray();
        var found = new Dictionary<string, int>();

        foreach (var row in rows) {
            var match = FurnitureRowParts().Match(row);

            if (match.Success && match.Groups[3].Value == "1" && fixes.ContainsKey(match.Groups[1].Value.Replace("\\'", "'"))) {
                found[match.Groups[1].Value.Replace("\\'", "'")] = int.Parse(match.Groups[2].Value);
            }
        }

        Assert.NotEmpty(found);
        Assert.All(found, entry => Assert.Equal(fixes[entry.Key], entry.Value));
    }

    // item_name, modes count, then has_furnidata after the vending ids, the height list and four flags.
    [GeneratedRegex(@"^\(\d+,'((?:[^'\\]|\\.)*)','(?:[^'\\]|\\.)*','\w',-?\d+,-?\d+,[^,]+,\d,\d,\d,-?\d+,\d,\d,\d,\d,\d,'(?:[^'\\]|\\.)*',-?\d+,(-?\d+),'(?:[^'\\]|\\.)*','(?:[^'\\]|\\.)*',-?\d+,-?\d+,\d,\d,(\d),")]
    private static partial Regex FurnitureRowParts();

    [RoomComponentDatabaseFact]
    public void AFreshInstallAndAMigratedHotelHoldTheSameFixedRows()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var fresh = "furni_counts_fresh_" + Guid.NewGuid().ToString("N");
        var migrated = "furni_counts_migrated_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{fresh}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE DATABASE `{migrated}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            var fixes = FixedClasses();

            foreach (var schema in new[] { fresh, migrated }) {
                using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
                connection.Open();

                foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                    connection.Execute(statement);
                }
            }

            using var hotel = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = migrated, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
            hotel.Open();

            // The hotel as it was before this migration: every fixed class has a count the fix replaces, and placed items sit above the new last state.
            hotel.Execute("UPDATE furniture SET interaction_modes_count = interaction_modes_count + 3 WHERE has_furnidata = 1 AND item_name IN @names", new { names = fixes.Select(fix => fix.Name).ToArray() });
            var shrinking = FixedClasses(MigrationFiles[^1]).First(fix => fix.Count >= 2 && fixes.Contains(fix));
            var baseItem = hotel.ExecuteScalar<uint>("SELECT id FROM furniture WHERE has_furnidata = 1 AND item_name = @name", new { name = shrinking.Name });
            hotel.Execute("INSERT INTO items (user_id, room_id, base_item, extra_data) VALUES (0, 0, @baseItem, @top), (0, 0, @baseItem, '0'), (0, 0, @baseItem, 'text')",
                new { baseItem, top = (shrinking.Count + 2).ToString() });

            foreach (var file in MigrationFiles) {
                foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo(file)))) {
                    hotel.Execute(statement);
                }
            }

            var query = "SELECT item_name, interaction_type, interaction_modes_count FROM furniture WHERE has_furnidata = 1 AND item_name IN @names ORDER BY item_name";
            var names = fixes.Select(fix => fix.Name).ToArray();
            using var freshConnection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = fresh }.ConnectionString);
            var freshRows = freshConnection.Query<(string, string, int)>(query, new { names }).ToList();
            var migratedRows = hotel.Query<(string, string, int)>(query, new { names }).ToList();

            Assert.Equal(fixes.Length, freshRows.Count);
            Assert.Equal(freshRows, migratedRows);
            Assert.All(migratedRows, row => Assert.Equal(fixes.Single(fix => fix.Name == row.Item1).Count, row.Item3));
            Assert.Equal(new[] { "0", (shrinking.Count - 1).ToString(), "text" }.Order(), hotel.Query<string>("SELECT extra_data FROM items WHERE base_item = @baseItem", new { baseItem }).Order());
        }
        finally {
            server.Execute($"DROP DATABASE `{fresh}`; DROP DATABASE `{migrated}`");
        }
    }
}
