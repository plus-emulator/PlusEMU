using System.Buffers.Binary;
using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using MySqlConnector;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Messenger;
using Plus.HabboHotel.Talents;
using Xunit;

namespace Plus.Tests;

[Collection("SharedDatabase")]
public sealed class AchievementPersistenceTests
{
    private const string Group = "ACH_Test";

    [RoomComponentDatabaseFact]
    public async Task LogoutDuringBadgeWaitDoesNotConsumeTheLevelOrAwardPoints()
    {
        using var fixture = new Fixture();
        using var enteredBadge = new ManualResetEventSlim();
        var releaseBadge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = fixture.Manager(() =>
        {
            enteredBadge.Set();

            return releaseBadge.Task;
        });
        var award = Task.Run(() => manager.ProgressAchievement(fixture.Client, Group, 1));
        Task? logout = null;

        try {
            Assert.True(enteredBadge.Wait(TimeSpan.FromSeconds(5)));
            logout = Task.Run(fixture.Habbo.OnDisconnect);
            await logout.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(fixture.Habbo.WalletClosed);
        }
        finally {
            releaseBadge.TrySetResult();
            await award.WaitAsync(TimeSpan.FromSeconds(5));

            if (logout != null) {
                await logout.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        Assert.False(await award);
        Assert.Equal(0, fixture.StoredLevels());
        Assert.Equal((0, 0), fixture.StoredAward());
        Assert.Equal((0, 0, 0), (fixture.Habbo.GetAchievementData(Group)!.Level,
            fixture.Habbo.Duckets, Assert.IsType<HabboStats>(fixture.Habbo.HabboStats).AchievementPoints));
        Assert.DoesNotContain(fixture.Sent, packet => packet.Header is
            ServerPacketHeader.HabboActivityPointNotificationComposer or ServerPacketHeader.AchievementScoreComposer);
        Assert.Empty(fixture.Broadcasts);
    }

    [RoomComponentDatabaseFact]
    public async Task DisposalDuringBadgeWaitRejectsTheLevelCommitWithoutClosingTheWallet()
    {
        using var fixture = new Fixture();
        using var enteredBadge = new ManualResetEventSlim();
        var releaseBadge = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var manager = fixture.Manager(() =>
        {
            enteredBadge.Set();

            return releaseBadge.Task;
        });
        var writes = 0;
        fixture.Database.BeforeConnection = () => Interlocked.Increment(ref writes);
        var award = Task.Run(() => manager.ProgressAchievement(fixture.Client, Group, 1));

        try {
            Assert.True(enteredBadge.Wait(TimeSpan.FromSeconds(5)));
            fixture.Habbo.Dispose();
            Assert.True(fixture.Habbo.AccessClosed);
            Assert.False(fixture.Habbo.WalletClosed);
            Assert.Same(fixture.Client, fixture.Habbo.Client);
        }
        finally {
            releaseBadge.TrySetResult();
            await award.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.False(await award);
        Assert.Equal(0, writes);
        Assert.Equal(0, fixture.StoredLevels());
        Assert.Equal((0, 0, 0), (fixture.Habbo.GetAchievementData(Group)!.Level,
            fixture.Habbo.Duckets, Assert.IsType<HabboStats>(fixture.Habbo.HabboStats).AchievementPoints));
        Assert.DoesNotContain(fixture.Sent, packet => packet.Header is
            ServerPacketHeader.HabboActivityPointNotificationComposer or ServerPacketHeader.AchievementScoreComposer
            or ServerPacketHeader.AchievementProgressedComposer);
        Assert.Empty(fixture.Broadcasts);
    }

    [RoomComponentDatabaseFact]
    public async Task SaveWaitsForAnAdmittedLevelCommitAndIncludesItsAward()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        using var enteredWrite = new ManualResetEventSlim();
        using var releaseWrite = new ManualResetEventSlim();
        using var startedSave = new ManualResetEventSlim();
        var connections = 0;
        fixture.Database.BeforeConnection = () =>
        {
            if (Interlocked.Increment(ref connections) != 1) {
                return;
            }

            enteredWrite.Set();
            Assert.True(releaseWrite.Wait(TimeSpan.FromSeconds(5)));
        };
        var award = Task.Run(() => manager.ProgressAchievement(fixture.Client, Group, 1));
        Task? save = null;
        bool savedWhileWritePaused;

        try {
            Assert.True(enteredWrite.Wait(TimeSpan.FromSeconds(5)));
            save = Task.Run(() => { startedSave.Set(); fixture.Habbo.Save(); });
            Assert.True(startedSave.Wait(TimeSpan.FromSeconds(5)));
            savedWhileWritePaused = await Task.WhenAny(save, Task.Delay(100)) == save;
        }
        finally {
            releaseWrite.Set();
            await award.WaitAsync(TimeSpan.FromSeconds(5));

            if (save != null) {
                await save.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }

        Assert.False(savedWhileWritePaused);
        Assert.True(await award);
        Assert.Equal(1, fixture.StoredLevels());
        Assert.Equal((20, 7), fixture.StoredAward());
        // The stored progress is the total score, which the level-up keeps.
        Assert.Equal((1, 1), (fixture.Habbo.GetAchievementData(Group)!.Level,
            fixture.Habbo.GetAchievementData(Group)!.Progress));
        Assert.Equal(new[] { (7, MessengerEventTypes.AchievementUnlocked, Group + "1") }, fixture.Broadcasts);
    }

    [RoomComponentDatabaseFact]
    public async Task CurrencySendCanDisconnectAndSaveTheCompleteAwardInLegacyPacketOrder()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var send = Assert.IsAssignableFrom<Func<System.Net.Sockets.SocketAsyncEventArgs, bool>>(fixture.Client.SendCallback);
        fixture.Client.SendCallback = args =>
        {
            var result = send(args);

            if (BinaryPrimitives.ReadUInt16BigEndian(args.MemoryBuffer.Span.Slice(4, 2)) ==
                ServerPacketHeader.HabboActivityPointNotificationComposer) {
                fixture.Habbo.OnDisconnect();
            }

            return result;
        };

        Assert.True(await Task.Run(() => manager.ProgressAchievement(fixture.Client, Group, 1))
            .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.True(fixture.Habbo.WalletClosed);
        Assert.Null(fixture.Habbo.Client);
        Assert.Equal(1, fixture.StoredLevels());
        Assert.Equal((20, 7), fixture.StoredAward());
        Assert.Equal(new[] { ServerPacketHeader.AchievementUnlockedComposer,
            ServerPacketHeader.HabboActivityPointNotificationComposer, ServerPacketHeader.AchievementScoreComposer,
            ServerPacketHeader.AchievementProgressedComposer }, fixture.Sent.Select(packet => packet.Header));
        var currency = new FlashIncomingPacket { Buffer = fixture.Sent[1].Payload };
        Assert.Equal((20, 20, 0), (currency.ReadInt(), currency.ReadInt(), currency.ReadInt()));
        Assert.False(currency.HasDataRemaining());
        Assert.Equal(7, BinaryPrimitives.ReadInt32BigEndian(fixture.Sent[2].Payload));
    }

    [RoomComponentDatabaseFact]
    public void FailedLevelWriteLeavesTheAwardAndLiveLevelUnchanged()
    {
        using var fixture = new Fixture();

        using (var connection = fixture.Database.Connection()) {
            connection.Execute("DROP TABLE user_achievements");
        }

        Assert.Throws<MySqlException>(() => fixture.Manager().ProgressAchievement(fixture.Client, Group, 1));

        Assert.Equal((0, 0, 0), (fixture.Habbo.GetAchievementData(Group)!.Level,
            fixture.Habbo.Duckets, Assert.IsType<HabboStats>(fixture.Habbo.HabboStats).AchievementPoints));
        // The unlock is announced only after the level is written.
        Assert.Empty(fixture.Sent);
        Assert.Empty(fixture.Broadcasts);
    }

    [RoomComponentDatabaseFact]
    public void ReachingSeveralThresholdsUnlocksEveryLevelInOrder()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager(achievement: Ladder());

        // Cumulative thresholds 1, 5 and 10 are all within a total of 12; 50 is not.
        Assert.True(manager.ProgressAchievement(fixture.Client, Group, 12));

        Assert.Equal(new[] { ("", Group + "1"), (Group + "1", Group + "2"), (Group + "2", Group + "3") }, fixture.Replaced);
        Assert.Equal((3, 12), fixture.Stored());
        Assert.Equal((23, 27), (fixture.Habbo.Duckets, Assert.IsType<HabboStats>(fixture.Habbo.HabboStats).AchievementPoints));
        Assert.Equal(new[] {
            ServerPacketHeader.AchievementUnlockedComposer, ServerPacketHeader.HabboActivityPointNotificationComposer, ServerPacketHeader.AchievementScoreComposer,
            ServerPacketHeader.AchievementUnlockedComposer, ServerPacketHeader.AchievementScoreComposer,
            ServerPacketHeader.AchievementUnlockedComposer, ServerPacketHeader.HabboActivityPointNotificationComposer, ServerPacketHeader.AchievementScoreComposer,
            ServerPacketHeader.AchievementProgressedComposer }, fixture.Sent.Select(packet => packet.Header));
        var unlocks = fixture.Sent.Where(packet => packet.Header == ServerPacketHeader.AchievementUnlockedComposer)
            .Select(packet => AchievementWireTests.ReadUnlock(packet.Payload)).ToList();
        Assert.Equal(new[] { (1, Group + "1", "", 7, 20), (2, Group + "2", Group + "1", 10, 0), (3, Group + "3", Group + "2", 10, 3) },
            unlocks.Select(unlock => (unlock.Level, unlock.Badge, unlock.RemovedBadge, unlock.Points, unlock.Reward)));
        // The bar continues from the last unlocked threshold towards the next one.
        var next = AchievementWireTests.ReadData(fixture.Sent[^1].Payload);
        Assert.Equal((4, Group + "4", 10, 50, 12, false), (next.Level, next.Badge, next.ScoreAtStart, next.ScoreLimit, next.CurrentPoints, next.FinalLevel));
        Assert.Equal(new[] { Group + "1", Group + "2", Group + "3" }, fixture.Broadcasts.Select(broadcast => broadcast.Value));
        fixture.Habbo.Save();
        Assert.Equal((23, 27), fixture.StoredAward());
    }

    [RoomComponentDatabaseFact]
    public void ProgressWithoutALevelUpIsWrittenWhenTheSessionIsSaved()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager(achievement: Ladder());
        Assert.True(manager.ProgressAchievement(fixture.Client, Group, 1));
        var writes = 0;
        fixture.Database.BeforeConnection = () => Interlocked.Increment(ref writes);
        fixture.Sent.Clear();

        Assert.False(manager.ProgressAchievement(fixture.Client, Group, 2));
        Assert.False(manager.ProgressAchievement(fixture.Client, Group, 1));

        Assert.Equal(0, writes);
        Assert.Equal((1, 1), fixture.Stored());
        Assert.Equal(new[] { 3, 4 }, fixture.Sent.Select(packet => AchievementWireTests.ReadData(packet.Payload).CurrentPoints));
        // fromBeginning replaces the total instead of adding to it, and never takes a level away.
        Assert.False(manager.ProgressAchievement(fixture.Client, Group, 2, fromBeginning: true));
        Assert.Equal((1, 2), (fixture.Habbo.GetAchievementData(Group)!.Level, fixture.Habbo.GetAchievementData(Group)!.Progress));

        fixture.Habbo.Save();

        Assert.Equal((1, 2), fixture.Stored());
    }

    [RoomComponentDatabaseFact]
    public void HiddenAchievementProgressesWithoutUpdatingTheAchievementList()
    {
        using var fixture = new Fixture();
        var hidden = Ladder();
        hidden.Visible = false;
        var manager = fixture.Manager(achievement: hidden);

        Assert.False(manager.ProgressAchievement(fixture.Client, Group, 0));
        Assert.Empty(fixture.Sent);
        Assert.True(manager.ProgressAchievement(fixture.Client, Group, 1));

        Assert.Equal((1, 1), fixture.Stored());
        Assert.Equal(new[] { ServerPacketHeader.AchievementUnlockedComposer, ServerPacketHeader.HabboActivityPointNotificationComposer,
            ServerPacketHeader.AchievementScoreComposer }, fixture.Sent.Select(packet => packet.Header));
        Assert.Equal(Group + "1", Assert.Single(fixture.Broadcasts).Value);
    }

    [RoomComponentDatabaseFact]
    public async Task LevelBadgeReplacesThePreviousRowAndKeepsItsWornSlot()
    {
        using var fixture = new Fixture();

        using (var connection = fixture.Database.Connection()) {
            connection.Execute("INSERT INTO badge_definitions(code,required_right) VALUES('ACH_Test1',''),('ACH_Test2',''),('ACH_Test3','');" +
                "INSERT INTO user_badges(user_id,badge_id,badge_slot) VALUES(7,'ACH_Test1',2),(7,'OTHER',1)");
        }

        var badges = new BadgeManager(fixture.Database, TestGameClientManager.Empty, TestLogging.For<BadgeManager>());
        await badges.Init();

        foreach (var badge in await badges.LoadBadgesForHabbo(7)) {
            fixture.Habbo.Inventory!.Badges.AddBadge(badge);
        }

        var manager = fixture.Manager(achievement: Ladder(), badges: badges);
        fixture.Habbo.Achievements.TryAdd(Group, new(Group, 1, 1));

        Assert.True(manager.ProgressAchievement(fixture.Client, Group, 4));

        using (var connection = fixture.Database.Connection()) {
            Assert.Equal(new[] { ("ACH_Test2", 2), ("OTHER", 1) },
                connection.Query<(string, int)>("SELECT badge_id,badge_slot FROM user_badges WHERE user_id=7 ORDER BY badge_id"));
        }

        var inventory = fixture.Habbo.Inventory!.Badges;
        Assert.False(inventory.HasBadge("ACH_Test1"));
        Assert.Equal(2, inventory.GetBadge("ACH_Test2")!.Slot);
        // The unlock makes AIR stop wearing the new badge, so the worn badges are sent after it.
        var headers = fixture.Sent.Select(packet => packet.Header).ToList();
        Assert.True(headers.IndexOf(ServerPacketHeader.HabboUserBadgesComposer) > headers.IndexOf(ServerPacketHeader.AchievementUnlockedComposer));
        var worn = new FlashIncomingPacket { Buffer = fixture.Sent[headers.IndexOf(ServerPacketHeader.HabboUserBadgesComposer)].Payload };
        Assert.Equal((7, 2), (worn.ReadInt(), worn.ReadInt()));
        Assert.Equal((1, "OTHER"), (worn.ReadInt(), worn.ReadString()));
        worn.ReadInt();
        worn.ReadInt();
        Assert.Equal((2, "ACH_Test2"), (worn.ReadInt(), worn.ReadString()));

        // The slot keeps following the newest level.
        Assert.True(manager.ProgressAchievement(fixture.Client, Group, 5));

        using (var connection = fixture.Database.Connection()) {
            Assert.Equal(new[] { ("ACH_Test3", 2), ("OTHER", 1) },
                connection.Query<(string, int)>("SELECT badge_id,badge_slot FROM user_badges WHERE user_id=7 ORDER BY badge_id"));
        }
    }

    // Cumulative thresholds 1, 5, 10 and 50 with mixed rewards.
    private static Achievement Ladder()
    {
        var achievement = new Achievement { Id = 1, GroupName = Group, Category = "identity" };
        achievement.AddLevel(new AchievementLevel(1, 20, 7, 1));
        achievement.AddLevel(new AchievementLevel(2, 0, 10, 5));
        achievement.AddLevel(new AchievementLevel(3, 3, 10, 10));
        achievement.AddLevel(new AchievementLevel(4, 0, 10, 50));

        return achievement;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MySqlConnection _admin;
        private readonly string _schema = "task_achievement_shutdown_" + Guid.NewGuid().ToString("N");
        public HabbiconDatabaseTests.TestDatabase Database { get; }
        public Habbo Habbo { get; }
        public FlashGameClient Client { get; }
        public List<(uint Header, byte[] Payload)> Sent { get; }
        public List<(int UserId, MessengerEventTypes Type, string Value)> Broadcasts { get; } = [];

        public Fixture()
        {
            var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
            _admin = new MySqlConnection(root);
            _admin.Execute($"CREATE DATABASE `{_schema}`");

            try {
                Database = new(new MySqlConnectionStringBuilder(root)
                {
                    Database = _schema,
                    AllowZeroDateTime = true,
                    ConvertZeroDateTime = true
                }.ConnectionString);
                using var connection = Database.Connection();
                var pristine = File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql"));

                foreach (var table in new[] { "users", "users_settings", "user_stats", "user_achievements", "user_badges", "badge_definitions" }) {
                    var definition = Regex.Match(pristine, $@"CREATE TABLE `{table}` \([\s\S]*?\) ENGINE=[^;]+;").Value;
                    Assert.NotEmpty(definition);
                    connection.Execute(definition);
                }

                connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/59_UserCurrencies.sql")));
                connection.Execute("ALTER TABLE users ADD bubble_id TINYINT NOT NULL DEFAULT 0; " +
                    "ALTER TABLE user_stats RENAME TO user_statistics; " +
                    "INSERT INTO users(id,username,auth_ticket) VALUES(7,'user','ticket'); " +
                    "INSERT INTO users_settings(user_id) VALUES(7); INSERT INTO user_statistics(id) VALUES(7)");
                Habbo = new Habbo
                {
                    Id = 7,
                    Username = "user",
                    Access = UserAccess.Empty,
                    SessionStartedAt = DateTimeOffset.UtcNow,
                    HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "old", 0),
                    // ProgressAchievement refuses a level-up without loaded statistics and inventory.
                    Inventory = new Plus.HabboHotel.Users.Inventory.InventoryComponent(),
                    Persistence = new UserPersistenceService(Database, TimeProvider.System, TestGameClientManager.Empty)
                };
                (Client, Sent) = HabbiconTestSupport.Client(Habbo);
                Habbo.Client = Client;
                var clients = CatalogSnapshotTestSupport.Proxy<IGameClientManager>((method, _) =>
                    method == "UnregisterClient" ? null : throw new InvalidOperationException(method));
                Habbo.SetHotelServices(clients, TestRoomManager.Unused);
            }
            catch {
                Dispose();
                throw;
            }
        }

        public List<(string Previous, string Badge)> Replaced { get; } = [];

        public AchievementManager Manager(Func<Task>? giveBadge = null, Achievement? achievement = null, IBadgeManager? badges = null)
        {
            badges ??= CatalogSnapshotTestSupport.Proxy<IBadgeManager>((method, args) =>
            {
                if (method != "ReplaceBadge") {
                    throw new InvalidOperationException(method);
                }

                Replaced.Add(((string)args[1]!, (string)args[2]!));

                return Replace(giveBadge?.Invoke() ?? Task.CompletedTask);
            });
            var messenger = CatalogSnapshotTestSupport.Proxy<IMessengerDataLoader>((method, args) =>
            {
                if (method != "BroadcastStatusUpdate") {
                    throw new InvalidOperationException(method);
                }

                Broadcasts.Add((((Habbo)args[0]!).Id, (MessengerEventTypes)args[1]!, (string)args[2]!));

                return null;
            });
            var manager = new AchievementManager(null!, Database, badges, CatalogSnapshotTestSupport.Proxy<ITalentTrackProgressionService>((method, _) => method == "Progress" ? null : throw new InvalidOperationException(method)),
                messenger);

            if (achievement == null) {
                achievement = new Achievement { Id = 1, GroupName = Group, Category = "identity" };
                achievement.AddLevel(new AchievementLevel(1, 20, 7, 1));
            }

            manager.Achievements.Add(Group, achievement);

            return manager;
        }

        private static async Task<bool> Replace(Task badge)
        {
            await badge;

            return false;
        }

        public int StoredLevels()
        {
            using var connection = Database.Connection();

            return connection.QuerySingle<int>("SELECT COUNT(*) FROM user_achievements WHERE userid=7 AND level=1");
        }

        public (int Level, int Progress) Stored()
        {
            using var connection = Database.Connection();

            return connection.QuerySingle<(int, int)>("SELECT level,progress FROM user_achievements WHERE userid=7");
        }

        public (int Duckets, int Score) StoredAward()
        {
            using var connection = Database.Connection();

            return connection.QuerySingle<(int, int)>(
                "SELECT COALESCE(c.amount,0),s.AchievementScore FROM users u JOIN user_statistics s ON s.id=u.id " +
                "LEFT JOIN user_currencies c ON c.user_id=u.id AND c.type=0 WHERE u.id=7");
        }

        public void Dispose()
        {
            try {
                _admin.Execute($"DROP DATABASE `{_schema}`");
            }
            finally {
                _admin.Dispose();
            }
        }
    }
}
