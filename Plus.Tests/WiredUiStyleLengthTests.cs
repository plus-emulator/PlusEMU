using System.Buffers.Binary;
using System.Data;
using System.Text;
using Dapper;
using MySqlConnector;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Users;
using Plus.Communication.Packets.Outgoing.Sound;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class WiredUiStyleLengthTests
{
    private const int MaxStyle = 30000;

    private static FlashIncomingPacket Save(string style) =>
        HabbiconTestSupport.Incoming(true, false, true, 0, false, true, style);

    private static UserProfileService Profiles(IDatabase database) =>
        new(null!, null!, null!, null!, database, TimeProvider.System, null!, null!, null!, null!);

    [Theory]
    [InlineData(31)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(MaxStyle)]
    public async Task TheRawFrameOfAStyleUpTo30000CharactersIsWritten(int length)
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new WiredSetPreferencesEvent(Profiles(new FailingDatabase()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Parse(session, Save(new string('a', length))));

        Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task TheRawFrameOfAStyleOver30000CharactersIsIgnored()
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new WiredSetPreferencesEvent(Profiles(new FailingDatabase()));

        await handler.Parse(session, Save(new string('a', MaxStyle + 1)));

        Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task AStyleThatTheFrameCutsShortIsIgnored()
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new WiredSetPreferencesEvent(Profiles(new FailingDatabase()));
        var head = HabbiconTestSupport.Incoming(true, false, true, 0, false, true).Buffer.ToArray();
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, MaxStyle);
        var frame = head.Concat(length).Concat(Encoding.UTF8.GetBytes(new string('a', 10))).ToArray();

        await handler.Parse(session, new FlashIncomingPacket { Buffer = frame });

        Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
        Assert.Empty(sent);
    }

    [Fact]
    public void TheFreshInstallStyleColumnHoldsTheLongestStyle()
    {
        var table = UsersSettingsTable();

        Assert.Contains("`wired_ui_style` text NOT NULL DEFAULT '',\n", table.Replace("\r\n", "\n"));
    }

    [RoomComponentDatabaseFact]
    public async Task TheMigrationWidensTheStyleColumnKeepsRowsAndCanRunTwice()
    {
        await InSchema((connection, _) =>
        {
            connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY, home_room INT NOT NULL DEFAULT 0); INSERT INTO users_settings VALUES(7,20),(8,0),(9,0)");
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/122_AccountWiredPreferences.sql")));
            connection.Execute("UPDATE users_settings SET wired_ui_style='volter', wired_menu_button=1 WHERE user_id=7; UPDATE users_settings SET wired_ui_style=REPEAT('x',64) WHERE user_id=9");
            var before = Fingerprint(connection);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(WidenFile));

            connection.Execute(migration);
            var once = Fingerprint(connection);
            connection.Execute(migration);

            Assert.Equal(before, once);
            Assert.Equal(once, Fingerprint(connection));
            var column = connection.QuerySingle("SELECT COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users_settings' AND COLUMN_NAME='wired_ui_style'");
            Assert.Equal(("text", "NO", "''"), ((string)column.COLUMN_TYPE, (string)column.IS_NULLABLE, (string)column.COLUMN_DEFAULT));
            connection.Execute("UPDATE users_settings SET wired_ui_style=REPEAT('é',@length) WHERE user_id=8", new { length = MaxStyle });
            Assert.Equal(MaxStyle, connection.ExecuteScalar<int>("SELECT CHAR_LENGTH(wired_ui_style) FROM users_settings WHERE user_id=8"));

            return Task.CompletedTask;
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheFreshInstallStyleColumnEqualsTheMigratedOne()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var fresh = "wired_style_fresh_" + Guid.NewGuid().ToString("N");
        var migrated = "wired_style_migrated_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{fresh}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci; CREATE DATABASE `{migrated}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            using (var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = fresh, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString)) {
                connection.Open();

                foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                    connection.Execute(statement);
                }
            }

            using (var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = migrated }.ConnectionString)) {
                connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY, home_room INT NOT NULL DEFAULT 0)");
                connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/122_AccountWiredPreferences.sql")));
                connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(WidenFile)));
            }

            var columns = server.Query<(string Schema, string Name, string Type, string Nullable, string? Default, string? Collation, int Position)>(
                "SELECT TABLE_SCHEMA, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLLATION_NAME, ORDINAL_POSITION FROM information_schema.COLUMNS " +
                "WHERE TABLE_SCHEMA IN (@fresh, @migrated) AND TABLE_NAME='users_settings' AND COLUMN_NAME LIKE 'wired\\_%' ORDER BY TABLE_SCHEMA, ORDINAL_POSITION", new { fresh, migrated }).ToList();
            var freshColumns = columns.Where(c => c.Schema == fresh).Select(c => (c.Name, c.Type, c.Nullable, c.Default, c.Collation)).ToList();
            var migratedColumns = columns.Where(c => c.Schema == migrated).Select(c => (c.Name, c.Type, c.Nullable, c.Default, c.Collation)).ToList();

            Assert.Equal(6, freshColumns.Count);
            Assert.Equal(freshColumns, migratedColumns);
            Assert.Equal("wired_ui_style", freshColumns[^1].Name);
        }
        finally {
            server.Execute($"DROP DATABASE `{fresh}`; DROP DATABASE `{migrated}`");
        }
    }

    [RoomComponentDatabaseFact]
    public async Task ALongStyleRoundTripsThroughTheRawFrameTheRowAndTheSettingsFrame()
    {
        await InSchema(async (connection, database) =>
        {
            connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY); INSERT INTO users_settings VALUES(7)");
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/122_AccountWiredPreferences.sql")));
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(WidenFile)));
            var habbo = new Habbo { Id = 7, ClientVolume = [75, 75, 75] };
            var (session, sent) = HabbiconTestSupport.Client(habbo);
            var handler = new WiredSetPreferencesEvent(Profiles(database));

            foreach (var style in new[] { new string('a', 31), new string('b', 1000), new string('c', MaxStyle), new string('é', MaxStyle) }) {
                sent.Clear();
                await handler.Parse(session, Save(style));

                Assert.Equal(style, connection.ExecuteScalar<string>("SELECT wired_ui_style FROM users_settings WHERE user_id=7"));
                Assert.Equal(style, habbo.WiredPreferences.UiStyle);
                session.Send(new SoundSettingsComposer(habbo.ClientVolume, false, false, false, 1, habbo.WiredPreferences, habbo.ChatDisplay));
                var frame = new FlashIncomingPacket { Buffer = Assert.Single(sent).Payload };

                for (var i = 0; i < 3; i++) {
                    frame.ReadInt();
                }

                for (var i = 0; i < 3; i++) {
                    frame.ReadBool();
                }

                frame.ReadInt();
                frame.ReadInt();

                for (var i = 0; i < 3; i++) {
                    frame.ReadBool();
                }

                frame.ReadInt();
                frame.ReadBool();
                frame.ReadBool();
                Assert.Equal(style, frame.ReadString());
            }

        });
    }

    [RoomComponentDatabaseFact]
    public async Task ALongStyleIsLoadedAtLogin()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "wired_style_load_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
            using var connection = database.Connection();
            connection.Open();

            foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                connection.Execute(statement);
            }

            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created) VALUES (7,'style_a','',UTC_TIMESTAMP(),UTC_TIMESTAMP())");
            connection.Execute("INSERT INTO users_settings(user_id,wired_ui_style) VALUES (7,@style)", new { style = new string('d', MaxStyle) });
            var factory = new UserDataFactory(null!, database, [], null!, null!, null!, new Plus.HabboHotel.Rooms.RoomVisitRecorder(database, TimeProvider.System),
                TimeProvider.System, TestRoomAchievements.Unused, TestGameClientManager.Empty, TestRoomManager.Unused);

            Assert.Equal(new string('d', MaxStyle), (await factory.GetUserDataByIdAsync(7))!.WiredPreferences.UiStyle);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private const string WidenFile = "Database/Migrations/124_WiredUiStyleLength.sql";

    private static string UsersSettingsTable()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var table = sql[sql.IndexOf("CREATE TABLE `users_settings`", StringComparison.Ordinal)..];

        return table[..table.IndexOf(") ENGINE", StringComparison.Ordinal)];
    }

    private static string Fingerprint(IDbConnection connection) => string.Join(";", connection.Query(
        "SELECT user_id, home_room, wired_menu_button, wired_ui_style FROM users_settings ORDER BY user_id").Select(row => $"{row.user_id}|{row.home_room}|{row.wired_menu_button}|{Convert.ToHexString(Encoding.UTF8.GetBytes((string)row.wired_ui_style))}"));

    private static async Task InSchema(Func<MySqlConnection, IDatabase, Task> body)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "wired_style_" + Guid.NewGuid().ToString("N");
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

    private sealed class FailingDatabase : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => throw new InvalidOperationException("Write failed");
    }

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
