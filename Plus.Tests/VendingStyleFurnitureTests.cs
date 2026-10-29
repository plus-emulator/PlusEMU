using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Plus.Database.Migrator;
using Xunit;

namespace Plus.Tests;

public sealed partial class VendingStyleFurnitureTests
{
    private const string Migration = "Database/Migrations/129_VendingStyleFurniture.sql";

    private static readonly string[] Classes =
    [
        "antique_c21_magnifyinglass", "dino_c15_nest", "lm_jackbox", "olympics_c16_nanarack", "paris_c15_vegstall1", "paris_c15_vegstall2",
        "paris_c15_vegstall3", "toaster", "tokyo_c18_snackdisplay"
    ];

    [GeneratedRegex(@"'((?:[^'\\]|''|\\.)*)'")]
    private static partial Regex Quoted();

    // item_name, then the interaction type after the usage flags that follow the sprite id.
    [GeneratedRegex(@"^\(\d+,'((?:[^'\\]|\\.)*)','(?:[^'\\]|\\.)*','\w',-?\d+,-?\d+,[^,]+,\d,\d,\d,-?\d+,\d,\d,\d,\d,\d,'(\w+)',")]
    private static partial Regex FurnitureInteraction();

    [Fact]
    public void TheMigrationNamesEachVendingStyleClassOnce()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo(Migration));
        var names = Quoted().Matches(sql[sql.IndexOf("IN (", StringComparison.Ordinal)..]).Select(match => match.Groups[1].Value).ToArray();

        Assert.Equal(Classes, names.Order());
        Assert.Contains("interaction_type = 'vendingmachine'", sql);
    }

    [Fact]
    public void TheFreshInstallGivesEachVendingStyleClassTheVendingMachineInteraction()
    {
        var sql = File.ReadAllLines(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var rows = sql.SkipWhile(line => !line.StartsWith("INSERT INTO `furniture`", StringComparison.Ordinal)).Where(line => line.StartsWith('(')).ToArray();
        var found = rows.Select(row => FurnitureInteraction().Match(row)).Where(match => match.Success && Classes.Contains(match.Groups[1].Value))
            .ToDictionary(match => match.Groups[1].Value, match => match.Groups[2].Value);

        Assert.Equal(Classes, found.Keys.Order());
        Assert.All(found, entry => Assert.Equal("vendingmachine", entry.Value));
    }

    [RoomComponentDatabaseFact]
    public void AFreshInstallAndAMigratedHotelHoldTheSameInteractionTypes()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var fresh = "vending_style_fresh_" + Guid.NewGuid().ToString("N");
        var migrated = "vending_style_migrated_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{fresh}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE DATABASE `{migrated}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            foreach (var schema in new[] { fresh, migrated }) {
                using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
                connection.Open();

                foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                    connection.Execute(statement);
                }
            }

            using var hotel = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = migrated, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
            hotel.Open();
            hotel.Execute("UPDATE furniture SET interaction_type = 'default' WHERE has_furnidata = 1 AND item_name IN @names", new { names = Classes });
            var statements = SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo(Migration))).ToArray();

            Assert.Equal(Classes.Length, statements.Sum(statement => hotel.Execute(statement)));
            Assert.Equal(0, statements.Sum(statement => hotel.Execute(statement)));

            var query = "SELECT item_name, interaction_type FROM furniture WHERE has_furnidata = 1 AND item_name IN @names ORDER BY item_name";
            using var freshConnection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = fresh }.ConnectionString);
            var freshRows = freshConnection.Query<(string, string)>(query, new { names = Classes }).ToList();
            var migratedRows = hotel.Query<(string, string)>(query, new { names = Classes }).ToList();

            Assert.Equal(Classes.Length, freshRows.Count);
            Assert.Equal(freshRows, migratedRows);
            Assert.All(migratedRows, row => Assert.Equal("vendingmachine", row.Item2));
        }
        finally {
            server.Execute($"DROP DATABASE `{fresh}`; DROP DATABASE `{migrated}`");
        }
    }
}
