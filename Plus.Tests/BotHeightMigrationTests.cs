using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

public sealed class BotHeightMigrationTests
{
    private const string Migration = "Database/Migrations/114_BotHeightDecimal.sql";

    [Fact]
    public void TheFreshInstallStoresABotHeightWithItsDecimals()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var bots = sql[sql.IndexOf("CREATE TABLE `bots` (", StringComparison.Ordinal)..];

        Assert.Contains("`z` double NOT NULL DEFAULT 0,", bots[..bots.IndexOf(';')]);
    }

    [RoomComponentDatabaseFact]
    public void TheMigrationKeepsStoredHeightsAndThenStoresDecimals()
    {
        using var connection = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        connection.Open();
        var schema = "bot_height_" + Guid.NewGuid().ToString("N");
        connection.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci");

        try {
            connection.Execute($"USE `{schema}`");
            connection.Execute("""
                CREATE TABLE bots (id INT UNSIGNED NOT NULL AUTO_INCREMENT, room_id INT NOT NULL DEFAULT 0, x INT NOT NULL DEFAULT 0,
                    y INT NOT NULL DEFAULT 0, z INT NOT NULL DEFAULT 0, PRIMARY KEY (id));
                INSERT INTO bots (id, room_id, x, y, z) VALUES (1, 42, 8, 8, 0), (2, 42, 3, 4, 3), (3, 0, 0, 0, -1);
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(Migration));

            connection.Execute(migration);
            connection.Execute(migration);

            Assert.Equal("double", connection.QuerySingle<string>(
                "SELECT COLUMN_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'bots' AND COLUMN_NAME = 'z'"));
            Assert.Equal(new (int, double)[] { (1, 0), (2, 3), (3, -1) }, connection.Query<(int, double)>("SELECT id, z FROM bots ORDER BY id"));
            connection.Execute("UPDATE bots SET z = @z WHERE id = 1", new { z = 0.2 });
            Assert.Equal(0.2, connection.QuerySingle<double>("SELECT z FROM bots WHERE id = 1"));
        }
        finally {
            connection.Execute("USE information_schema");
            connection.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
