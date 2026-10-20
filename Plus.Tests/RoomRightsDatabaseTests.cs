using System.Runtime.CompilerServices;
using Dapper;
using MySqlConnector;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed class RoomRightsDatabaseTests
{
    [RoomComponentDatabaseFact]
    public void GroupRoomLoadsSavedRightsAndGivingThemAgainKeepsOneRow()
    {
        using var connection = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        connection.Open();
        var schema = "room_rights_" + Guid.NewGuid().ToString("N");
        connection.Execute($"CREATE DATABASE `{schema}`");

        try {
            connection.Execute($"USE `{schema}`");
            connection.Execute("""
                CREATE TABLE room_rights (room_id INT UNSIGNED NOT NULL, user_id INT UNSIGNED NOT NULL, PRIMARY KEY (room_id, user_id));
                INSERT INTO room_rights VALUES (42, 8), (42, 9), (43, 10);
                """);
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!)
            { Database = schema }.ConnectionString);
            var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
            room.Id = 42;
            room.Group = new(20, "group", "", "", 42, 7, null, 0, 1, 1, 0, false, GroupMembershipSnapshot.ForOwner(7));
            var component = new RoomRightsComponent(database);
            component.Initiate(room);

            component.Initiated();

            Assert.Equal([8, 9], room.UsersWithRights.Order());

            new RoomRightsStore(database).Assign(42, 8);

            Assert.Equal(1, connection.QuerySingle<int>($"SELECT COUNT(*) FROM `{schema}`.room_rights WHERE room_id = 42 AND user_id = 8"));
        }
        finally {
            connection.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class ProbeDatabase(string connectionString) : Plus.Database.IDatabase
    {
        public bool IsConnected() => true;
        public System.Data.IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
