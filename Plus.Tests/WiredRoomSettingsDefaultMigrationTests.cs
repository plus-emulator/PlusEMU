using Dapper;
using MySqlConnector;
using Xunit;

namespace Plus.Tests;

public sealed class WiredRoomSettingsDefaultMigrationTests
{
    [RoomComponentDatabaseFact]
    public void ExistingRoomsWithoutASavedRowKeepRightsHolderAndGroupAdminAccessAndSavedRowsStay()
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")) { Database = "information_schema", Pooling = false };
        var schema = "wired_settings_default_" + Guid.NewGuid().ToString("N")[..12];
        using var admin = new MySqlConnection(options.ConnectionString);
        admin.Open();
        admin.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            using var connection = new MySqlConnection(options.ConnectionString);
            connection.Open();
            connection.Execute("CREATE TABLE rooms (id INT(10) UNSIGNED PRIMARY KEY) ENGINE=InnoDB");
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/18_AddWiredRoomSettings.sql")));
            connection.Execute("""
                INSERT INTO rooms (id) VALUES (1), (2), (3);
                INSERT INTO room_wired_settings (room_id, inspect_mask, modify_mask, timezone) VALUES (2, 14, 8, 'Europe/Berlin');
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/121_WiredRoomSettingsKeepExistingRights.sql"));
            connection.Execute(migration);
            connection.Execute(migration);

            var rows = connection.Query<(uint RoomId, int Inspect, int Modify, string Timezone)>(
                "SELECT room_id, inspect_mask, modify_mask, timezone FROM room_wired_settings ORDER BY room_id").ToList();

            Assert.Equal([(1u, 10, 10, ""), (2u, 14, 8, "Europe/Berlin"), (3u, 10, 10, "")], rows);
        }
        finally {
            admin.Execute($"DROP DATABASE `{schema}`");
        }
    }
}
