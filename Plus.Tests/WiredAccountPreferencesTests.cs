using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Users;
using Plus.Communication.Packets.Outgoing.Sound;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class WiredAccountPreferencesTests
{
    private static readonly WiredAccountPreferences Everything = new(true, true, true, true, true, "volter");

    [Fact]
    public void TheWireFrameHasTheShowAllFlagAndTheStyleAfterTheWhisperFlag()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new SoundSettingsComposer([75, 75, 75], false, false, false, 3, Everything).Compose(packet);

        Assert.Equal(new object[] { 75, 75, 75, false, false, false, 3, 0, true, true, true, 0, true, true, "volter" }, packet.Writes);
    }

    [Fact]
    public void TheWhisperFlagAndTheShowAllFlagKeepTheirOwnPlaces()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new SoundSettingsComposer([75, 75, 75], false, false, false, 3, new WiredAccountPreferences(false, true, false, true, false, "volter")).Compose(packet);

        Assert.Equal(new object[] { 75, 75, 75, false, false, false, 3, 0, false, true, false, 0, true, false, "volter" }, packet.Writes);
    }

    [Fact]
    public void ADefaultAccountSendsAllFlagsOffAndAnEmptyStyle()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new SoundSettingsComposer([75, 75, 75], false, false, false, 3, new Habbo().WiredPreferences).Compose(packet);

        Assert.Equal(new object[] { 75, 75, 75, false, false, false, 3, 0, false, false, false, 0, false, false, "" }, packet.Writes);
    }

    [Theory]
    [InlineData("0100000000000000000000", true, false, false, false, false, "")]
    [InlineData("0101000000000000000000", true, true, false, false, false, "")]
    [InlineData("0101010000000000010000", true, true, true, false, true, "")]
    [InlineData("0001000000000001010006766F6C746572", false, true, false, true, true, "volter")]
    [InlineData("0001000000000001000006766F6C746572", false, true, false, true, false, "volter")]
    public async Task TheClientFrameSavesTheSixFields(string frame, bool menu, bool inspect, bool playTest, bool whisper, bool showAll, string style)
    {
        var profiles = new RecordingProfiles();
        var packet = new FlashIncomingPacket { Buffer = Convert.FromHexString(frame) };

        await new WiredSetPreferencesEvent(profiles).Parse(null!, packet);

        Assert.Equal(new WiredAccountPreferences(menu, inspect, playTest, whisper, showAll, style), profiles.Saved);
    }

    [Theory]
    [InlineData("01010100000000")]
    [InlineData("010101000000000001")]
    [InlineData("01010100000000000100000100")]
    public async Task ATruncatedOrOverlongFrameSavesNothing(string frame)
    {
        var profiles = new RecordingProfiles();

        await new WiredSetPreferencesEvent(profiles).Parse(null!, new FlashIncomingPacket { Buffer = Convert.FromHexString(frame) });

        Assert.Null(profiles.Saved);
    }

    [Fact]
    public async Task AFailedWriteKeepsTheLivePreferencesAndSendsNothing()
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var profiles = new UserProfileService(null!, null!, null!, null!, new FailingDatabase(), TimeProvider.System, null!, null!, null!, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => profiles.SetWiredPreferences(session, Everything));

        Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
        Assert.Empty(sent);
    }

    [Fact]
    public async Task AStyleLongerThanTheColumnIsNotWritten()
    {
        var habbo = new Habbo { Id = 7 };
        var (session, sent) = HabbiconTestSupport.Client(habbo);
        var profiles = new UserProfileService(null!, null!, null!, null!, new FailingDatabase(), TimeProvider.System, null!, null!, null!, null!);

        await profiles.SetWiredPreferences(session, Everything with { UiStyle = new string('a', WiredAccountPreferences.MaxUiStyleLength + 1) });

        Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
        Assert.Empty(sent);
    }

    [Fact]
    public void TheFreshInstallSchemaHasTheSixColumnsWithOffDefaults()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var table = sql[sql.IndexOf("CREATE TABLE `users_settings`", StringComparison.Ordinal)..];
        table = table[..table.IndexOf(") ENGINE", StringComparison.Ordinal)];

        foreach (var column in new[] { "wired_menu_button", "wired_inspect_button", "wired_play_test", "wired_whisper_disabled", "wired_show_all_notifications" }) {
            Assert.Contains($"`{column}` tinyint(1) NOT NULL DEFAULT 0", table);
        }

        Assert.Contains("`wired_ui_style` varchar(64) NOT NULL DEFAULT ''", table);
    }

    [RoomComponentDatabaseFact]
    public async Task TheMigrationAddsTheColumnsKeepsRowsAndCanRunTwice()
    {
        using var server = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        var schema = "wired_prefs_migration_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { Database = schema }.ConnectionString);
            using var connection = database.Connection();
            connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY, home_room INT NOT NULL DEFAULT 0); INSERT INTO users_settings VALUES(7,20),(8,0)");
            var migration = File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile));
            connection.Execute(migration);
            connection.Execute("UPDATE users_settings SET wired_menu_button=1, wired_ui_style='volter' WHERE user_id=7");
            connection.Execute(migration);

            var seven = connection.QuerySingle("SELECT * FROM users_settings WHERE user_id=7");
            var eight = connection.QuerySingle("SELECT * FROM users_settings WHERE user_id=8");
            Assert.Equal((20, 1L, "volter"), ((int)seven.home_room, Convert.ToInt64(seven.wired_menu_button), (string)seven.wired_ui_style));
            Assert.Equal((0L, 0L, 0L, 0L, 0L, ""), (Convert.ToInt64(eight.wired_menu_button), Convert.ToInt64(eight.wired_inspect_button),
                Convert.ToInt64(eight.wired_play_test), Convert.ToInt64(eight.wired_whisper_disabled), Convert.ToInt64(eight.wired_show_all_notifications), (string)eight.wired_ui_style));
            await Task.CompletedTask;
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    [RoomComponentDatabaseFact]
    public async Task TheServiceWritesOnlyTheOwnRowAndRefusesAMissingRow()
    {
        using var server = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        var schema = "wired_prefs_service_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { Database = schema }.ConnectionString);
            using var connection = database.Connection();
            connection.Execute("CREATE TABLE users_settings(user_id INT PRIMARY KEY); INSERT INTO users_settings VALUES(7),(8)");
            connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo(MigrationFile)));
            var habbo = new Habbo { Id = 7 };
            var (session, sent) = HabbiconTestSupport.Client(habbo);
            var profiles = new UserProfileService(null!, null!, null!, null!, database, TimeProvider.System, null!, null!, null!, null!);

            await profiles.SetWiredPreferences(session, Everything);

            Assert.Equal(Everything, habbo.WiredPreferences);
            Assert.Equal((1, 1, 1, 1, 1, "volter"), Row(connection, 7));
            Assert.Equal((0, 0, 0, 0, 0, ""), Row(connection, 8));

            await profiles.SetWiredPreferences(session, WiredAccountPreferences.Default);
            Assert.Equal((0, 0, 0, 0, 0, ""), Row(connection, 7));

            connection.Execute("DELETE FROM users_settings WHERE user_id=7");
            await Assert.ThrowsAsync<DBConcurrencyException>(() => profiles.SetWiredPreferences(session, Everything));
            Assert.Equal(WiredAccountPreferences.Default, habbo.WiredPreferences);
            Assert.Empty(sent);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    [RoomComponentDatabaseFact]
    public async Task LoginLoadsTheStoredPreferencesFromTheAccountRow()
    {
        SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        var schema = "wired_prefs_load_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var database = new ProbeDatabase(new MySqlConnectionStringBuilder(root) { Database = schema, AllowZeroDateTime = true, ConvertZeroDateTime = true }.ConnectionString);
            using var connection = database.Connection();
            connection.Open();

            foreach (var statement in SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))) {
                connection.Execute(statement);
            }

            connection.Execute("INSERT INTO users (id,username,auth_ticket,last_online,account_created) VALUES (7,'prefs_a','',UTC_TIMESTAMP(),UTC_TIMESTAMP()),(8,'prefs_b','',UTC_TIMESTAMP(),UTC_TIMESTAMP()); " +
                "INSERT INTO users_settings(user_id,wired_menu_button,wired_play_test,wired_show_all_notifications,wired_ui_style) VALUES (7,1,1,1,'volter'); INSERT INTO users_settings(user_id) VALUES (8)");
            var factory = new UserDataFactory(null!, database, [], null!, null!, null!, new Plus.HabboHotel.Rooms.RoomVisitRecorder(database, TimeProvider.System),
                TimeProvider.System, TestRoomAchievements.Unused, TestGameClientManager.Empty, TestRoomManager.Unused);

            Assert.Equal(new WiredAccountPreferences(true, false, true, false, true, "volter"), (await factory.GetUserDataByIdAsync(7))!.WiredPreferences);
            Assert.Equal(WiredAccountPreferences.Default, (await factory.GetUserDataByIdAsync(8))!.WiredPreferences);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private const string MigrationFile = "Database/Migrations/122_AccountWiredPreferences.sql";

    private static (int, int, int, int, int, string) Row(IDbConnection connection, int userId)
    {
        var row = connection.QuerySingle("SELECT * FROM users_settings WHERE user_id=@userId", new { userId });

        return (Convert.ToInt32(row.wired_menu_button), Convert.ToInt32(row.wired_inspect_button), Convert.ToInt32(row.wired_play_test),
            Convert.ToInt32(row.wired_whisper_disabled), Convert.ToInt32(row.wired_show_all_notifications), (string)row.wired_ui_style);
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

    private sealed class RecordingProfiles : IUserProfileService
    {
        public WiredAccountPreferences? Saved { get; private set; }
        public void ShowUserObject(Plus.HabboHotel.GameClients.GameClient session) { }
        public Task SetChatPreference(Plus.HabboHotel.GameClients.GameClient session, bool enabled) => Task.CompletedTask;
        public Task SetMessengerInvitePreference(Plus.HabboHotel.GameClients.GameClient session, bool enabled) => Task.CompletedTask;
        public Task SetSoundVolumes(Plus.HabboHotel.GameClients.GameClient session, SoundVolumeRequest request) => Task.CompletedTask;
        public void UpdateFigure(Plus.HabboHotel.GameClients.GameClient session, FigureUpdateRequest request) { }
        public void ApplyMannequin(Plus.HabboHotel.GameClients.GameClient session, FigureUpdateRequest request) { }
        public void ChangeMotto(Plus.HabboHotel.GameClients.GameClient session, string motto) { }
        public void SetFocusPreference(Plus.HabboHotel.GameClients.GameClient session, bool enabled) { }
        public Task SetChatStylePreference(Plus.HabboHotel.GameClients.GameClient session, int bubbleId) => Task.CompletedTask;
        public void SetFriendBarState(Plus.HabboHotel.GameClients.GameClient session, int state) { }
        public Task SetWiredPreferences(Plus.HabboHotel.GameClients.GameClient session, WiredAccountPreferences preferences)
        {
            Saved = preferences;

            return Task.CompletedTask;
        }
    }
}
