using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

public sealed class EffectBoxStateMigrationDatabaseTests
{
    [RoomComponentDatabaseFact]
    public void EffectBoxesStuckInTheOpeningStateCloseAgainAndOtherFurniKeepTheirState()
    {
        using var connection = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        connection.Open();
        var schema = "effect_box_state_" + Guid.NewGuid().ToString("N");
        connection.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci");

        try {
            connection.Execute($"USE `{schema}`");
            connection.Execute("""
                CREATE TABLE furniture (id INT PRIMARY KEY, item_name VARCHAR(70) NOT NULL, interaction_type VARCHAR(50) NOT NULL);
                CREATE TABLE items (id INT PRIMARY KEY, user_id INT NOT NULL, room_id INT UNSIGNED NOT NULL DEFAULT 0, base_item INT NOT NULL,
                    extra_data TEXT NULL);

                INSERT INTO furniture VALUES (1, 'fxbox_fx16', 'effect_box'), (2, 'fxbox_fx152', 'effect_box'), (3, 'ads_lamp', 'default'),
                    (4, 'fxbox_fx92', 'gate');
                INSERT INTO items VALUES
                    (10, 7, 42, 1, '1'), (11, 7, 0, 2, '1'), (12, 7, 42, 1, '0'), (13, 7, 42, 2, ''),
                    (20, 7, 42, 3, '1'), (21, 7, 42, 4, '1');
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/108_CloseStuckEffectBoxes.sql"));

            connection.Execute(migration);

            var expected = new (int, string)[] { (10, "0"), (11, "0"), (12, "0"), (13, ""), (20, "1"), (21, "1") };
            Assert.Equal(expected, connection.Query<(int, string)>("SELECT id, extra_data FROM items ORDER BY id"));

            connection.Execute(migration);

            Assert.Equal(expected, connection.Query<(int, string)>("SELECT id, extra_data FROM items ORDER BY id"));
        }
        finally {
            connection.Execute("USE information_schema");
            connection.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
