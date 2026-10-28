using System.Data;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Preferences;
using Plus.Communication.Packets.Incoming.Sound;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Sound;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class ChatDisplayPreferencesTests
{
    private const string MigrationFile = "Database/Migrations/125_AccountChatDisplayPreferences.sql";

    private static UserProfileService Profiles(IDatabase database) =>
        new(null!, null!, null!, null!, database, TimeProvider.System, null!, null!, null!, null!);

    private static PacketManager Manager(IDatabase database) => new(
        [new SetChatDisplayPreferencesEvent(Profiles(database)), new SetOnlineIndicatorPreferenceEvent(Profiles(database)),
         new SetChatStylePreferenceEvent(Profiles(database)), new GetSoundSettingsEvent()], NullLogger<PacketManager>.Instance);

    private static FlashIncomingPacket Request() => new() { Buffer = Array.Empty<byte>() };

    private static FlashIncomingPacket Frame(params object[] values) => HabbiconTestSupport.Incoming(values);

    private static (int, int, int, int, int) Tail(byte[] payload)
    {
        var frame = new FlashIncomingPacket { Buffer = payload };

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
        frame.ReadString();

        return (frame.ReadInt(), frame.ReadInt(), frame.ReadInt(), frame.ReadInt(), frame.ReadInt());
    }

    [Fact]
    public void TheClientSavesHaveTheOfficialIdsAndHandlers()
    {
        using var manager = Manager(new FailingDatabase());
        var revision = new Plus.Communication.Revisions.RevisionsCache().InternalRevision;

        Assert.Equal(2359u, ClientPacketHeader.SetChatDisplayPreferencesEvent);
        Assert.Equal(1917u, ClientPacketHeader.SetOnlineIndicatorPreferenceEvent);
        Assert.Equal(3519u, ClientPacketHeader.SetChatStylePreferenceEvent);

        foreach (var id in new uint[] { 2359, 1917, 3519 }) {
            Assert.True(manager.IsRegistered(id));
            Assert.Single(revision.IncomingHeaders, pair => pair.Value == id);
            Assert.Equal(id, revision.IncomingIdToInternalIdMapping[id]);
        }
    }

    [Fact]
    public void AnAccountThatNeverSavedHasTheClientDefaults()
    {
        Assert.Equal(new ChatDisplayPreferences(0, 0, 1, 1, 0), new Habbo().ChatDisplay);
        Assert.Equal((0, 0, 1, 1, 0), Tail(Convert.FromHexString(ComposeHex(new Habbo()))));
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0)]
    [InlineData(4, 1, 2, 2, 2)]
    [InlineData(2, 1, 0, 2, 1)]
    public void TheSettingsFrameCarriesTheStoredValuesInTheOfficialOrder(int size, int mode, int width, int speed, int online)
    {
        var habbo = new Habbo { ClientVolume = [1, 2, 3], ChatDisplay = new ChatDisplayPreferences(size, mode, width, speed, online) };

        Assert.Equal((size, mode, width, speed, online), Tail(Convert.FromHexString(ComposeHex(habbo))));
    }

    [Fact]
    public async Task TheGetSoundSettingsAnswerCarriesTheStoredValues()
    {
        var habbo = new Habbo { Id = 7, ClientVolume = [1, 2, 3], ChatDisplay = new ChatDisplayPreferences(3, 1, 2, 0, 2) };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        using var manager = Manager(new FailingDatabase());

        await manager.TryExecutePacket(session, 929, Request());

        Assert.Equal((3, 1, 2, 0, 2), Tail(Assert.Single(sent).Payload));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 2, 2)]
    [InlineData(1, 1, 1)]
    public async Task AChatPreferencesFrameWithValidValuesReachesTheDatabaseWrite(int mode, int width, int speed)
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new SetChatDisplayPreferencesEvent(Profiles(new FailingDatabase()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Parse(session, Frame(false, mode, width, speed)));

        Assert.Equal(ChatDisplayPreferences.Default, habbo.ChatDisplay);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 3, 1)]
    [InlineData(0, -1, 1)]
    [InlineData(0, 1, 3)]
    [InlineData(0, 1, -1)]
    public async Task AChatPreferencesFrameWithAValueOutOfRangeIsIgnored(int mode, int width, int speed)
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new SetChatDisplayPreferencesEvent(Profiles(new FailingDatabase()));

        await handler.Parse(session, Frame(false, mode, width, speed));

        Assert.Equal(ChatDisplayPreferences.Default, habbo.ChatDisplay);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task ATruncatedChatPreferencesFrameIsIgnored()
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new SetChatDisplayPreferencesEvent(Profiles(new FailingDatabase()));

        await handler.Parse(session, Frame(false, 1, 2));
        await handler.Parse(session, Frame());

        Assert.Equal(ChatDisplayPreferences.Default, habbo.ChatDisplay);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AnOnlineIndicatorFrameWithAValidValueReachesTheDatabaseWrite(int value)
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new SetOnlineIndicatorPreferenceEvent(Profiles(new FailingDatabase()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Parse(session, Frame(value)));

        Assert.Equal(ChatDisplayPreferences.Default, habbo.ChatDisplay);
        Assert.Empty(sent);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-1)]
    public async Task AnOnlineIndicatorFrameWithAValueOutOfRangeIsIgnored(int value)
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var handler = new SetOnlineIndicatorPreferenceEvent(Profiles(new FailingDatabase()));

        await handler.Parse(session, Frame(value));
        await handler.Parse(session, Frame());

        Assert.Equal(ChatDisplayPreferences.Default, habbo.ChatDisplay);
        Assert.Empty(sent);
    }

    [Fact]
    public void TheFreshInstallTableHoldsTheFiveColumnsLast()
    {
        var table = UsersSettingsTable().Replace("\r\n", "\n");

        Assert.Contains("`chat_font_size` tinyint(4) NOT NULL DEFAULT 0,\n  `chat_mode` tinyint(4) NOT NULL DEFAULT 0,\n  `chat_bubble_width` tinyint(4) NOT NULL DEFAULT 1,\n" +
            "  `chat_scroll_speed` tinyint(4) NOT NULL DEFAULT 1,\n  `friend_online_indicator` tinyint(4) NOT NULL DEFAULT 0,\n  PRIMARY KEY", table);
    }

    [RoomComponentDatabaseFact]
    public async Task TheMigrationAddsTheFiveColumnsWithTheClientDefaultsKeepsRowsAndCanRunTwice()
    {
        await InSchema((connection, _) =>
        {
            connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY, home_room INT NOT NULL DEFAULT 0, volume VARCHAR(30) NOT NULL DEFAULT '100,100,100'); INSERT INTO users_settings VALUES(7,20,'10,20,30'),(8,0,'100,100,100')");
            var before = Fingerprint(connection);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile));

            connection.Execute(migration);
            var once = Fingerprint(connection);
            connection.Execute("UPDATE users_settings SET chat_mode=1, chat_bubble_width=2, chat_scroll_speed=0, chat_font_size=3, friend_online_indicator=2 WHERE user_id=7");
            var saved = Fingerprint(connection);
            connection.Execute(migration);

            Assert.Equal("7|20|10,20,30;8|0|100,100,100", before);
            Assert.Equal("7|20|10,20,30|0|0|1|1|0;8|0|100,100,100|0|0|1|1|0", once);
            Assert.Equal("7|20|10,20,30|3|1|2|0|2;8|0|100,100,100|0|0|1|1|0", saved);
            Assert.Equal(saved, Fingerprint(connection));
            var columns = connection.Query("SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users_settings' AND ORDINAL_POSITION>3 ORDER BY ORDINAL_POSITION")
                .Select(c => ((string)c.COLUMN_NAME, (string)c.COLUMN_TYPE, (string)c.IS_NULLABLE, (string)c.COLUMN_DEFAULT)).ToList();
            Assert.Equal([("chat_font_size", "tinyint(4)", "NO", "0"), ("chat_mode", "tinyint(4)", "NO", "0"), ("chat_bubble_width", "tinyint(4)", "NO", "1"),
                ("chat_scroll_speed", "tinyint(4)", "NO", "1"), ("friend_online_indicator", "tinyint(4)", "NO", "0")], columns);

            return Task.CompletedTask;
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheFreshInstallColumnsEqualTheMigratedOnes()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var fresh = "chat_display_fresh_" + Guid.NewGuid().ToString("N");
        var migrated = "chat_display_migrated_" + Guid.NewGuid().ToString("N");
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
                connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile)));
            }

            var columns = server.Query<(string Schema, string Name, string Type, string Nullable, string? Default, string? Collation)>(
                "SELECT TABLE_SCHEMA, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLUMN_DEFAULT, COLLATION_NAME FROM information_schema.COLUMNS " +
                "WHERE TABLE_SCHEMA IN (@fresh, @migrated) AND TABLE_NAME='users_settings' AND COLUMN_NAME IN ('chat_font_size','chat_mode','chat_bubble_width','chat_scroll_speed','friend_online_indicator') ORDER BY TABLE_SCHEMA, ORDINAL_POSITION",
                new { fresh, migrated }).ToList();
            var freshColumns = columns.Where(c => c.Schema == fresh).Select(c => (c.Name, c.Type, c.Nullable, c.Default, c.Collation)).ToList();
            var migratedColumns = columns.Where(c => c.Schema == migrated).Select(c => (c.Name, c.Type, c.Nullable, c.Default, c.Collation)).ToList();

            Assert.Equal(5, freshColumns.Count);
            Assert.Equal(freshColumns, migratedColumns);
            var order = server.Query<string>("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=@fresh AND TABLE_NAME='users_settings' ORDER BY ORDINAL_POSITION DESC LIMIT 5", new { fresh }).Reverse().ToList();
            Assert.Equal(["chat_font_size", "chat_mode", "chat_bubble_width", "chat_scroll_speed", "friend_online_indicator"], order);
        }
        finally {
            server.Execute($"DROP DATABASE `{fresh}`; DROP DATABASE `{migrated}`");
        }
    }

    [RoomComponentDatabaseFact]
    public async Task TheRawSavesAreStoredPerAccountAndComeBackInTheGetSoundSettingsAnswer()
    {
        await InSchema(async (connection, database) =>
        {
            connection.Execute("CREATE TABLE users(id INT PRIMARY KEY, bubble_id INT NOT NULL DEFAULT 0); CREATE TABLE users_settings(user_id INT PRIMARY KEY); INSERT INTO users VALUES(7,0),(8,0); INSERT INTO users_settings VALUES(7),(8)");
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile)));
            var habbo = new Habbo { Id = 7, ClientVolume = [75, 75, 75] };
            var (session, sent) = HabbiconTestSupport.Client(habbo);
            using var manager = Manager(database);

            await manager.TryExecutePacket(session, 929, Request());
            Assert.Equal((0, 0, 1, 1, 0), Tail(sent[^1].Payload));

            await manager.TryExecutePacket(session, 2359, Frame(false, 1, 2, 0));
            await manager.TryExecutePacket(session, 929, Request());
            Assert.Equal((0, 1, 2, 0, 0), Tail(sent[^1].Payload));
            Assert.Equal((0, 1, 2, 0, 0), Row(connection, 7));

            await manager.TryExecutePacket(session, 1917, Frame(2));
            await manager.TryExecutePacket(session, 3519, Frame(0, 3));
            await manager.TryExecutePacket(session, 929, Request());
            Assert.Equal((3, 1, 2, 0, 2), Tail(sent[^1].Payload));
            Assert.Equal((3, 1, 2, 0, 2), Row(connection, 7));
            Assert.Equal((0, 0, 1, 1, 0), Row(connection, 8));
            Assert.Equal(new ChatDisplayPreferences(3, 1, 2, 0, 2), habbo.ChatDisplay);

            var count = sent.Count;
            await manager.TryExecutePacket(session, 2359, Frame(false, 2, 9, -1));
            await manager.TryExecutePacket(session, 1917, Frame(7));
            await manager.TryExecutePacket(session, 3519, Frame(0, 9));
            await manager.TryExecutePacket(session, 2359, Frame(false, 1));
            await manager.TryExecutePacket(session, 1917, Frame());

            Assert.Equal(count, sent.Count);
            Assert.Equal((3, 1, 2, 0, 2), Row(connection, 7));
            Assert.Equal(new ChatDisplayPreferences(3, 1, 2, 0, 2), habbo.ChatDisplay);

            await manager.TryExecutePacket(session, 3519, Frame(0));
            Assert.Equal((3, 1, 2, 0, 2), Row(connection, 7));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheStoredValuesAreLoadedAtLoginAndAnAccountWithoutSavesLoadsTheDefaults()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "chat_display_load_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
            using var connection = database.Connection();
            connection.Open();

            foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                connection.Execute(statement);
            }

            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created) VALUES (7,'chat_a','',UTC_TIMESTAMP(),UTC_TIMESTAMP()),(8,'chat_b','',UTC_TIMESTAMP(),UTC_TIMESTAMP())");
            connection.Execute("INSERT INTO users_settings(user_id,chat_font_size,chat_mode,chat_bubble_width,chat_scroll_speed,friend_online_indicator) VALUES (7,4,1,2,0,2)");
            connection.Execute("INSERT INTO users_settings(user_id) VALUES (8)");
            var factory = new UserDataFactory(null!, database, [], null!, null!, null!, new Plus.HabboHotel.Rooms.RoomVisitRecorder(database, TimeProvider.System),
                TimeProvider.System, TestRoomAchievements.Unused, TestGameClientManager.Empty, TestRoomManager.Unused);

            Assert.Equal(new ChatDisplayPreferences(4, 1, 2, 0, 2), (await factory.GetUserDataByIdAsync(7))!.ChatDisplay);
            Assert.Equal(ChatDisplayPreferences.Default, (await factory.GetUserDataByIdAsync(8))!.ChatDisplay);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static string ComposeHex(Habbo habbo)
    {
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        session.Send(SoundSettingsComposer.For(habbo));

        return Convert.ToHexString(Assert.Single(sent).Payload);
    }

    private static (int, int, int, int, int) Row(IDbConnection connection, int userId)
    {
        var row = connection.QuerySingle("SELECT chat_font_size, chat_mode, chat_bubble_width, chat_scroll_speed, friend_online_indicator FROM users_settings WHERE user_id=@userId", new { userId });

        return ((int)row.chat_font_size, (int)row.chat_mode, (int)row.chat_bubble_width, (int)row.chat_scroll_speed, (int)row.friend_online_indicator);
    }

    private static string UsersSettingsTable()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var table = sql[sql.IndexOf("CREATE TABLE `users_settings`", StringComparison.Ordinal)..];

        return table[..table.IndexOf(") ENGINE", StringComparison.Ordinal)];
    }

    private static string Fingerprint(IDbConnection connection)
    {
        var columns = connection.Query<string>("SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='users_settings' ORDER BY ORDINAL_POSITION").ToList();

        return string.Join(";", connection.Query($"SELECT CONCAT_WS('|', {string.Join(", ", columns.Select(c => $"`{c}`"))}) AS line FROM users_settings ORDER BY user_id").Select(row => (string)row.line));
    }

    private static async Task InSchema(Func<MySqlConnection, IDatabase, Task> body)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "chat_display_" + Guid.NewGuid().ToString("N");
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
