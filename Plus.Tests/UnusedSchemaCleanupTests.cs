using System.Data;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;
using Plus.HabboHotel.Users.Authentication;
using Xunit;

namespace Plus.Tests;

public sealed class UnusedSchemaCleanupTests
{
    private const string MigrationFile = "Database/Migrations/131_DropUnusedColumnsAndLogs.sql";
    private static readonly string[] DroppedTables = ["housekeeping_log", "housekeeping_online_peaks", "furni_editor_log"];
    private static readonly (string Table, string Column)[] DroppedColumns =
    [
        ("users", "vip"), ("user_statistics", "GiftsGiven"), ("user_statistics", "GiftsReceived"),
        ("user_statistics", "quest_progress"), ("user_statistics", "forum_posts"), ("room_models", "public_items")
    ];

    [Fact]
    public void TheFreshInstallHasNoUnusedTablesOrColumns()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")).Replace("\r\n", "\n");

        // A new hotel has no old Wired settings to keep.
        foreach (var table in DroppedTables.Append("wired_items")) {
            Assert.DoesNotContain($"CREATE TABLE `{table}` (", sql);
        }

        foreach (var (table, column) in DroppedColumns) {
            Assert.DoesNotContain($"\n  `{column}` ", CreateTable(sql, table));
        }
    }

    // A new hotel has no ledger yet, so the emulator looks for each old migration's marker. None may look unapplied.
    [SchemaMigrationDatabaseFact]
    public async Task ANewHotelStartsWithoutRunningAnOldMigration()
    {
        var server = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SchemaMigrationDatabaseFactAttribute.Variable));
        var schema = "unused_schema_start_" + Guid.NewGuid().ToString("N");
        using var root = new MySqlConnection(server.ConnectionString);
        root.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            var scripts = MigrationScripts.Load();
            var configuration = new DatabaseConfiguration
            {
                Hostname = server.Server, Port = server.Port, Username = server.UserID, Password = server.Password, Name = schema,
                Migrations = new MigrationConfiguration { DumpCommand = string.Empty }
            };
            var migrator = new SchemaMigrator(configuration, NullLogger<SchemaMigrator>.Instance, scripts,
                () => File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")));

            Assert.True(await migrator.Run(MigrationMode.Startup));

            var unsafeToRerun = scripts.Where(script => script.Probe != null && !script.Rerunnable).Select(script => script.Id).ToHashSet();
            var applied = root.Query<string>($"SELECT migration FROM `{schema}`.schema_migrations WHERE source = 'applied'").ToList();
            Assert.Contains("131_DropUnusedColumnsAndLogs.sql", applied);
            Assert.DoesNotContain(applied, unsafeToRerun.Contains);
        }
        finally {
            root.Execute($"DROP DATABASE `{schema}`");
        }
    }

    [RoomComponentDatabaseFact]
    public void TheMigrationDropsTheUnusedSchemaKeepsOldWiredSettingsAndCanRunTwice()
    {
        InSchema("unused_schema_", connection =>
        {
            connection.Execute("""
                CREATE TABLE users (id INT PRIMARY KEY, username VARCHAR(25), vip TINYINT(1) DEFAULT 1);
                CREATE TABLE user_statistics (id INT PRIMARY KEY, Respect INT, GiftsGiven INT, GiftsReceived INT, quest_id INT, quest_progress INT, forum_posts INT);
                CREATE TABLE room_models (id VARCHAR(100) PRIMARY KEY, heightmap TEXT, public_items TEXT);
                CREATE TABLE housekeeping_log (id INT PRIMARY KEY);
                CREATE TABLE housekeeping_online_peaks (day DATE PRIMARY KEY, peak INT);
                CREATE TABLE furni_editor_log (id INT PRIMARY KEY);
                CREATE TABLE wired_items (id INT PRIMARY KEY, items TEXT);
                INSERT INTO users VALUES (7, 'johndoe', 1);
                INSERT INTO user_statistics VALUES (7, 3, 1, 2, 9, 4, 5);
                INSERT INTO room_models VALUES ('model_a', '000', '');
                INSERT INTO wired_items VALUES (12, '13;14');
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile));

            connection.Execute(migration);
            connection.Execute(migration);

            foreach (var table in DroppedTables) {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table", new { table }));
            }

            foreach (var (table, column) in DroppedColumns) {
                Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column", new { table, column }));
            }

            Assert.Equal((7, "johndoe"), connection.QuerySingle<(int, string)>("SELECT id, username FROM users"));
            Assert.Equal((3, 9), connection.QuerySingle<(int, int)>("SELECT Respect, quest_id FROM user_statistics WHERE id = 7"));
            Assert.Equal("000", connection.QuerySingle<string>("SELECT heightmap FROM room_models WHERE id = 'model_a'"));
            Assert.Equal("13;14", connection.QuerySingle<string>("SELECT items FROM wired_items WHERE id = 12"));
        });
    }

    // Login, logout and the floor plan editor read and write the tables that lost columns.
    [RoomComponentDatabaseFact]
    public async Task AnAccountAndAFloorPlanAreLoadedAndSavedOnTheFreshInstall()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        await InFreshInstall(async (connection, database) =>
        {
            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created) VALUES (7,'johndoe','',UTC_TIMESTAMP(),UTC_TIMESTAMP()); " +
                "INSERT INTO users_settings (user_id) VALUES (7); INSERT INTO rooms (id,owner,model_name,caption) VALUES (40,7,'model_a','Room')");
            var factory = new UserDataFactory(null!, database, [], null!, null!, null!, new RoomVisitRecorder(database, TimeProvider.System),
                TimeProvider.System, TestRoomAchievements.Unused, TestGameClientManager.Empty, TestRoomManager.Unused);

            var habbo = (await factory.GetUserDataByIdAsync(7))!;
            habbo.HabboStats = await new HabboStatsService(database).LoadHabboStats(7);
            habbo.HabboStats.Respect = 4;
            habbo.HabboStats.QuestId = 3;
            habbo.SessionStartedAt = DateTimeOffset.UtcNow;
            new UserPersistenceService(database, TimeProvider.System, TestGameClientManager.Empty).Save(habbo);
            var stats = await new HabboStatsService(database).LoadHabboStats(7);

            Assert.Equal("johndoe", habbo.Username);
            Assert.Equal((4, 3), (stats.Respect, stats.QuestId));

            var decision = FloorPlanSave.Evaluate("000\r000\r000", 1, 1, 2, 0, 0, 8, [], new Dictionary<(int X, int Y), FloorPlanSave.FloorTile>());
            new FloorPlanStore(database).Save(40, "custom_40", decision);

            Assert.Equal(("000\r000\r000", "custom_40"), connection.QuerySingle<(string, string)>(
                "SELECT m.heightmap, r.model_name FROM rooms r JOIN room_models m ON m.id = r.model_name WHERE r.id = 40"));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task ADisabledModerationPresetIsNotOffered()
    {
        await InFreshInstall(async (connection, database) =>
        {
            connection.Execute("DELETE FROM moderation_presets; INSERT INTO moderation_presets (type, message, enabled) VALUES " +
                "('user', 'Shown to users', 1), ('user', 'Hidden from users', 0), ('room', 'Shown for rooms', 1), ('room', 'Hidden for rooms', 0)");
            var moderation = new ModerationManager(database, NullLogger<ModerationManager>.Instance, new SsoTicketStore(database, TimeProvider.System),
                new SharedTestClients(), new AccountSessionGate(), TimeProvider.System);

            await moderation.Start();

            Assert.Equal(["Shown to users"], moderation.UserMessagePresets);
            Assert.Equal(["Shown for rooms"], moderation.RoomMessagePresets);
        });
    }

    private static string CreateTable(string sql, string table)
    {
        var start = sql.IndexOf($"CREATE TABLE `{table}` (", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{table} is missing");

        return sql[start..sql.IndexOf(") ENGINE", start, StringComparison.Ordinal)];
    }

    private static void InSchema(string prefix, Action<MySqlConnection> body)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = prefix + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString);
            connection.Open();
            body(connection);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static async Task InFreshInstall(Func<MySqlConnection, ProbeDatabase, Task> body)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "unused_schema_fresh_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            var connectionString = new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                connection.Execute(statement);
            }

            await body(connection, new ProbeDatabase(connectionString));
        }
        finally {
            // Idle pooled connections of the services under test would block the drop.
            MySqlConnection.ClearAllPools();
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
