using System.Data;
using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Items.MysteryBoxes;
using Xunit;

namespace Plus.Tests;

public sealed class MysteryBoxDatabaseTests
{
    private const string PrizeMigration = "Database/Migrations/178_MysteryBoxPrizes.sql";

    private static readonly string[] Prizes =
    [
        "xmas12_floortile", "xmas12_bardesk", "xmas12_pork", "xmas12_nutcracker", "xmas12_table", "xmas12_santathrone", "xmas12_npole",
        "xmas12_chair", "xmas12_pillar1", "xmas12_pillar2", "xmas12_pillar3", "xmas12_barrel", "xmas12_snack", "xmas12_floortile2",
        "xmas12_cfloor", "xmas12_worktable", "xmas12_cfloorbig", "xmas12_wall_in", "xmas12_window", "xmas12_wall_out", "xmas12_door",
        "xmas12_torch", "xmas12_fireplace"
    ];

    [Fact]
    public void TheFreshInstallHasEveryChristmasPrizeOnceAndTheFireplaceIsRare()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var furniture = Regex.Matches(Data(sql, "furniture"), @"^\((\d+),'(xmas12_[a-z0-9_]+)',", RegexOptions.Multiline)
            .ToDictionary(match => match.Groups[2].Value, match => int.Parse(match.Groups[1].Value));
        var prizes = Regex.Matches(Data(sql, "mystery_box_prizes"), @"^\(\d+,(\d+),(\d+)\)", RegexOptions.Multiline)
            .Select(match => (FurnitureId: int.Parse(match.Groups[1].Value), Weight: int.Parse(match.Groups[2].Value)))
            .ToList();

        Assert.Equal(Prizes.Length, prizes.Count);
        Assert.Equal(prizes.Count, prizes.Select(prize => prize.FurnitureId).Distinct().Count());

        foreach (var name in Prizes) {
            var weight = name == "xmas12_fireplace" ? 1 : 5;
            Assert.Contains((furniture[name], weight), prizes);
        }
    }

    [RoomComponentDatabaseFact]
    public void ThePrizeMigrationMatchesExactNamesOnceAndARerunAddsNothing()
    {
        WithSchema("mystery_box_prizes_", (connection, _) =>
        {
            connection.Execute("""
                INSERT INTO furniture (id, item_name) VALUES
                    (10, 'xmas12_chair'), (11, 'xmas12_fireplace'), (12, 'XMAS12_TABLE'), (13, 'xmas12_chair_other'), (14, 'mystery_box');
                INSERT INTO mystery_box_prizes (furniture_id, weight) VALUES (14, 3);
                """);

            var migration = File.ReadAllText(HabbiconPacketTests.Repo(PrizeMigration));
            connection.Execute(migration);
            connection.Execute(migration);

            Assert.Equal(new (uint, int)[] { (10, 5), (11, 1), (14, 3) }, connection.Query<(uint, int)>(
                "SELECT furniture_id, weight FROM mystery_box_prizes ORDER BY furniture_id").ToArray());
        });
    }

    [RoomComponentDatabaseFact]
    public void GivingAKeySavesTheColoursAndLocksTheUsersBoxesThatNoGuestUnlocked()
    {
        WithSchema("mystery_box_keys_", (connection, connectionString) =>
        {
            connection.Execute("""
                INSERT INTO users (id, username) VALUES (7, 'owner'), (8, 'guest');
                INSERT INTO furniture (id, item_name, interaction_type) VALUES (1, 'mystery_box', 'mystery_box'), (2, 'chair', 'default');
                INSERT INTO items (id, user_id, base_item, extra_data) VALUES
                    (100, 7, 1, '6'), (101, 7, 1, '5'), (102, 7, 1, '0'), (103, 7, 2, '6'), (104, 8, 1, '6');
                """);
            var store = new MysteryBoxStore(new RawDatabase(connectionString), null!);

            store.GiveKey(7, new("blue", "red"));
            store.GiveKey(7, new("blue", "green"));

            Assert.Equal(new MysteryBoxColors("blue", "green"), store.Colors(7));
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_mystery_boxes"));
            Assert.Equal(new (uint, string)[] { (100, "4"), (101, "5"), (102, "4"), (103, "6"), (104, "6") }, connection.Query<(uint, string)>(
                "SELECT id, extra_data FROM items ORDER BY id").ToArray());
        });
    }

    private static void WithSchema(string prefix, Action<MySqlConnection, string> test)
    {
        var server = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = prefix + Guid.NewGuid().ToString("N");

        using (var setup = new MySqlConnection(server)) {
            setup.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }

        try {
            var connectionString = new MySqlConnectionStringBuilder(server) { Database = schema }.ConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.Open();
            var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
            connection.Execute("SET FOREIGN_KEY_CHECKS=0");

            foreach (var table in new[] { "users", "furniture", "items", "user_mystery_boxes", "mystery_box_prizes" }) {
                connection.Execute(WiredMigrationSchema.Table(fresh, table));
            }

            connection.Execute("SET FOREIGN_KEY_CHECKS=1");
            test(connection, connectionString);
        }
        finally {
            using var cleanup = new MySqlConnection(server);
            cleanup.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static string Data(string sql, string table)
    {
        var start = sql.IndexOf($"-- Dumping data for table `{table}`", StringComparison.Ordinal);

        if (start < 0) {
            return "";
        }

        var end = sql.IndexOf("-- Dumping data for table", start + 1, StringComparison.Ordinal);

        return end < 0 ? sql[start..] : sql[start..end];
    }

    private sealed class RawDatabase(string connectionString) : IDatabase
    {
        public IDbConnection Connection() => new MySqlConnection(connectionString);
        public bool IsConnected() => true;
    }
}
