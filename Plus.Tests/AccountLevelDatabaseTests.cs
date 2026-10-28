using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class AccountLevelDatabaseTests
{
    private const string MigrationFile = "Database/Migrations/126_AccountLevel.sql";

    [Fact]
    public void TheFreshInstallUsersTableEndsWithTheAccountLevel()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")).Replace("\r\n", "\n");
        var table = sql[sql.IndexOf("CREATE TABLE `users` (", StringComparison.Ordinal)..];

        Assert.Contains("  `last_change` datetime DEFAULT NULL,\n  `account_level` int(11) NOT NULL DEFAULT 8,\n  PRIMARY KEY (`id`),",
            table[..table.IndexOf(") ENGINE", StringComparison.Ordinal)]);
    }

    // Every existing account gets level 8, which keeps the 3125 cost cap rooms had before. Stored levels survive a rerun.
    [RoomComponentDatabaseFact]
    public async Task TheMigrationGivesEveryAccountLevelEightKeepsRowsAndCanRunTwice()
    {
        await InSchema("account_level_", (connection, _) =>
        {
            connection.Execute("CREATE TABLE users(id INT PRIMARY KEY, username VARCHAR(20) NOT NULL); INSERT INTO users VALUES(7,'a'),(8,'b')");
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile));

            connection.Execute(migration);
            Assert.Equal("7|a|8;8|b|8", Rows(connection));
            connection.Execute("UPDATE users SET account_level=13 WHERE id=7");
            connection.Execute(migration);

            Assert.Equal("7|a|13;8|b|8", Rows(connection));
            Assert.Equal(("int(11)", "NO", "8"), connection.QuerySingle<(string, string, string)>("SELECT COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT " +
                "FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users' AND COLUMN_NAME='account_level'"));
            connection.Execute("INSERT INTO users(id,username) VALUES(9,'c')");
            Assert.Equal(8, connection.ExecuteScalar<int>("SELECT account_level FROM users WHERE id=9"));

            return Task.CompletedTask;
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheFreshInstallColumnEqualsTheMigratedOne()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var fresh = "account_level_fresh_" + Guid.NewGuid().ToString("N");
        var migrated = "account_level_migrated_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{fresh}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE DATABASE `{migrated}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            using (var connection = FreshInstall(root, fresh)) { }

            using (var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = migrated }.ConnectionString)) {
                connection.Execute("CREATE TABLE users(id INT PRIMARY KEY)");
                connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile)));
            }

            var columns = server.Query<(string Schema, string Type, string Nullable, string? Default)>(
                "SELECT TABLE_SCHEMA, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT FROM information_schema.COLUMNS " +
                "WHERE TABLE_SCHEMA IN (@fresh, @migrated) AND TABLE_NAME='users' AND COLUMN_NAME='account_level' ORDER BY TABLE_SCHEMA",
                new { fresh, migrated }).ToList();

            Assert.Equal(2, columns.Count);
            Assert.Equal((columns[0].Type, columns[0].Nullable, columns[0].Default), (columns[1].Type, columns[1].Nullable, columns[1].Default));
        }
        finally {
            server.Execute($"DROP DATABASE `{fresh}`; DROP DATABASE `{migrated}`");
        }
    }

    // The login and profile load reads the stored level, and a room reads its owner's level with the room row.
    [RoomComponentDatabaseFact]
    public async Task TheStoredLevelIsLoadedWithTheAccountAndWithEveryRoomOfTheOwner()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "account_level_load_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            using var connection = FreshInstall(root, schema);
            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created,account_level) VALUES (7,'level_a','',UTC_TIMESTAMP(),UTC_TIMESTAMP(),13)");
            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created) VALUES (8,'level_b','',UTC_TIMESTAMP(),UTC_TIMESTAMP())");
            connection.Execute("INSERT INTO users_settings(user_id) VALUES (7),(8)");
            connection.Execute("INSERT INTO rooms (id,owner,model_name,caption) VALUES (40,7,'model_a','high'),(41,8,'model_a','default')");
            var database = new ProbeDatabase(SchemaConnectionString(root, schema));
            var factory = new UserDataFactory(null!, database, [], null!, null!, null!, new RoomVisitRecorder(database, TimeProvider.System),
                TimeProvider.System, TestRoomAchievements.Unused, TestGameClientManager.Empty, TestRoomManager.Unused);

            Assert.Equal(13, (await factory.GetUserDataByIdAsync(7))!.AccountLevel);
            Assert.Equal(8, (await factory.GetUserDataByIdAsync(8))!.AccountLevel);

            var model = new RoomModel("model_a", 0, 0, 0, 0, "00\r00", 0, 0, false);
            var rooms = connection.Query<RoomDataRow>(RoomDataMapping.SelectRoom + "WHERE rooms.id IN (40, 41) ORDER BY rooms.id")
                .Select(row => RoomDataMapping.CreateData(row, model, true)).ToList();
            Assert.Equal([(40u, 13), (41u, 8)], rooms.Select(room => (room.Id, room.OwnerAccountLevel)));
            Assert.Equal([13, 8], rooms.Select(room => new RoomData(room).OwnerAccountLevel));
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static MySqlConnection FreshInstall(string root, string schema)
    {
        var connection = new MySqlConnection(SchemaConnectionString(root, schema));
        connection.Open();

        foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
            connection.Execute(statement);
        }

        return connection;
    }

    private static string SchemaConnectionString(string root, string schema) =>
        new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString;

    private static string Rows(IDbConnection connection) =>
        string.Join(";", connection.Query<string>("SELECT CONCAT_WS('|', id, username, account_level) FROM users ORDER BY id"));

    private static async Task InSchema(string prefix, Func<MySqlConnection, IDatabase, Task> body)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = prefix + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            var connectionString = new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString;
            using var connection = new MySqlConnection(connectionString);
            await body(connection, new ProbeDatabase(connectionString));
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
