using System.Collections.Immutable;
using System.Data;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Quests;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Quests;
using Plus.Core;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Quests.DailyTasks;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;
using Plus.HabboHotel.Users.Inventory;
using Xunit;

namespace Plus.Tests;

public sealed class DailyTaskTests
{
    // 2026-11-01 22:00 UTC is 2026-11-02 00:00 in a UTC+2 server zone; the tests run at 20:00 UTC (22:00 local).
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("plus+2", TimeSpan.FromHours(2), "plus+2", "plus+2");
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 1);

    [Fact]
    public void HeadersAreTheAirIds()
    {
        Assert.Equal(4100u, ClientPacketHeader.GetDailyTasksEvent);
        Assert.Equal(4101u, ClientPacketHeader.ClaimDailyTaskEvent);
        Assert.Equal(1699u, ServerPacketHeader.ActiveDailyTasksComposer);
        Assert.Equal(3276u, ServerPacketHeader.DailyTasksAddedComposer);
        Assert.Equal(82u, ServerPacketHeader.DailyTaskUpdatedComposer);
    }

    [Fact]
    public void ComposersWriteTheAirFieldOrder()
    {
        var reward = new DailyTaskReward(DailyTaskReward.Currency, "0", "", 25);
        var task = new DailyTaskWire(0x1_0000_0002L, "1741850663726_G", "chat_with_someone", true, "_v2", "", 10, 4,
            DailyTaskStatus.InProgress, 7200, [reward]);
        object[] expected = [1, 1, 2, "1741850663726_G", "chat_with_someone", true, "_v2", "", 10, 4, (byte)0, 7200, 1, (short)8, "0", "", 25];

        var active = new HabbiconTestSupport.RecordingPacket();
        new ActiveDailyTasksComposer([task]).Compose(active);
        Assert.Equal(expected, active.Writes);

        var added = new HabbiconTestSupport.RecordingPacket();
        new DailyTasksAddedComposer([task]).Compose(added);
        Assert.Equal(expected, added.Writes);

        var updated = new HabbiconTestSupport.RecordingPacket();
        new DailyTaskUpdatedComposer(77, 3, DailyTaskStatus.Claimed, -1).Compose(updated);
        Assert.Equal(new object[] { 0, 77, 3, (byte)2, -1 }, updated.Writes);
    }

    [Fact]
    public void TheDayEndsAtTheServersMidnight()
    {
        Assert.Equal(new DateOnly(2026, 11, 1), DailyTaskRules.DayOf(Now, Zone));
        Assert.Equal(new DateOnly(2026, 11, 2), DailyTaskRules.DayOf(Now.AddHours(2), Zone));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 22, 0, 0, TimeSpan.Zero), DailyTaskRules.ResetAfter(Today, Zone));

        var task = new UserDailyTask(1, 1, Today, DailyTaskRules.ResetAfter(Today, Zone), 0, DailyTaskStatus.InProgress);
        Assert.Equal(7200, DailyTaskRules.SecondsLeft(task, Now));
        Assert.Equal(1, DailyTaskRules.SecondsLeft(task, Now.AddSeconds(7199.5)));
        Assert.Equal(-1, DailyTaskRules.SecondsLeft(task, Now.AddHours(2)));
    }

    [Fact]
    public void PicksMixActionsAndRespectTheCount()
    {
        var pool = new[] { Definition(1, "chat"), Definition(2, "chat"), Definition(3, "wave"), Definition(4, "dance"), Definition(5, "swim", weight: 0) };

        for (var seed = 0; seed < 50; seed++) {
            var picks = DailyTaskRules.Pick(pool, 3, new Random(seed));
            Assert.Equal(3, picks.Count);
            Assert.Equal(3, picks.Select(definition => definition.ActionType).Distinct().Count());
            Assert.DoesNotContain(picks, definition => definition.Id == 5);
        }

        Assert.Equal(4, DailyTaskRules.Pick(pool, 9, new Random(1)).Count);
        Assert.Empty(DailyTaskRules.Pick(pool, 0, new Random(1)));
    }

    [Fact]
    public void ProgressCapsAtTheRequiredRepeats()
    {
        var definition = Definition(1, "chat", repeats: 3);
        var task = new UserDailyTask(1, 1, Today, Now.AddHours(1), 1, DailyTaskStatus.InProgress);

        Assert.Equal((2, DailyTaskStatus.InProgress), DailyTaskRules.Plan(task, definition, 1));
        Assert.Equal((3, DailyTaskStatus.Completed), DailyTaskRules.Plan(task, definition, 5));
        Assert.Null(DailyTaskRules.Plan(task, definition, 0));

        task.Status = DailyTaskStatus.Completed;
        Assert.Null(DailyTaskRules.Plan(task, definition, 1));
    }

    [Fact]
    public void ParametersNarrowTheAction()
    {
        var drinks = Definition(1, "find_hand_item", parameters: "1, 8,10");

        Assert.True(drinks.Matches("find_hand_item", "8"));
        Assert.False(drinks.Matches("find_hand_item", "2"));
        Assert.False(drinks.Matches("find_hand_item", null));
        Assert.False(drinks.Matches("wave", "8"));
        Assert.True(Definition(2, "wave").Matches("wave", "anything"));
    }

    [Fact]
    public void GrantsPayCurrenciesAndBadgesAndDoubleDucketsForClubMembers()
    {
        var definition = Definition(1, "chat", rewards:
        [
            new(DailyTaskReward.Currency, "0", "", 25),
            new(DailyTaskReward.Currency, "-1", "", 3),
            new(DailyTaskReward.Currency, "5", "", 2),
            new(DailyTaskReward.Badge, "ACH_Test1", "", 1)
        ]);

        var plain = Assert.IsType<DailyTaskGrant>(DailyTaskRules.Grant(definition, false));
        Assert.Equal(3, plain.Credits);
        Assert.Equal(new Dictionary<int, int> { [0] = 25, [5] = 2 }, plain.Points);
        Assert.Equal(["ACH_Test1"], plain.Badges.ToArray());
        Assert.Equal(50, DailyTaskRules.Grant(definition, true)!.Points[ActivityPointType.Duckets]);

        Assert.Null(DailyTaskRules.Grant(Definition(2, "chat", rewards: [new(DailyTaskReward.FloorItem, "13", "", 1)]), false));
        Assert.Null(DailyTaskRules.Grant(Definition(3, "chat", rewards: [new(DailyTaskReward.Currency, "-3", "", 1)]), false));
        Assert.Null(DailyTaskRules.Grant(Definition(4, "chat", rewards: [new(DailyTaskReward.Currency, "0", "", 0)]), false));
    }

    [Fact]
    public void LoginListHandsOutTheDaysTasksOnce()
    {
        var (manager, store, _) = Manager();
        var (client, sent) = HabbiconTestSupport.Client(User());

        manager.SendActive(client);

        var tasks = ActiveTasks(sent);
        Assert.Equal(3, tasks.Count);
        Assert.All(tasks, task => Assert.Equal(7200, task.SecondsLeft));
        Assert.DoesNotContain(tasks, task => task.IsBonus);
        Assert.Equal(3, store.Rows.Count);
        sent.Clear();

        manager.SendActive(client);
        Assert.Equal(tasks.Select(task => task.TaskId), ActiveTasks(sent).Select(task => task.TaskId));
        Assert.Equal(3, store.Rows.Count);
    }

    [Fact]
    public void ProgressUpdatesMatchingTasksAndCompletingAllOpensTheBonus()
    {
        var (manager, store, _) = Manager(regular: [Definition(1, "chat", repeats: 2), Definition(2, "find_hand_item", parameters: "8")]);
        var (client, sent) = HabbiconTestSupport.Client(User());
        manager.SendActive(client);
        sent.Clear();

        manager.Progress(client, "wave");
        manager.Progress(client, "find_hand_item", 1, "2");
        Assert.Empty(sent);

        manager.Progress(client, "chat");
        var update = Assert.Single(sent);
        Assert.Equal(ServerPacketHeader.DailyTaskUpdatedComposer, update.Header);
        Assert.Equal((1, DailyTaskStatus.InProgress), (store.Definition(1).Progress, store.Definition(1).Status));
        sent.Clear();

        manager.Progress(client, "chat", 5);
        manager.Progress(client, "find_hand_item", 1, "8");
        Assert.Equal([ServerPacketHeader.DailyTaskUpdatedComposer, ServerPacketHeader.DailyTaskUpdatedComposer, ServerPacketHeader.DailyTasksAddedComposer],
            sent.Select(packet => (uint)packet.Header));
        Assert.Equal((2, DailyTaskStatus.Completed), (store.Definition(1).Progress, store.Definition(1).Status));
        Assert.Equal(DailyTaskStatus.Completed, store.Definition(2).Status);
        Assert.Single(store.Rows, row => row.DefinitionId == 9);
        sent.Clear();

        manager.SendActive(client);
        var tasks = ActiveTasks(sent);
        Assert.Equal(3, tasks.Count);
        Assert.True(tasks[^1].IsBonus);
    }

    [Fact]
    public async Task ClaimPaysOnceAndAnnouncesTheClaim()
    {
        var (manager, store, _) = Manager(regular: [Definition(1, "chat")]);
        var habbo = User();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        var claims = 0;
        manager.TaskClaimed += _ => claims++;
        manager.SendActive(client);
        var taskId = (int)ActiveTasks(sent)[0].TaskId;

        // Not completed yet: nothing is paid, the update re-enables the client's view.
        await new ClaimDailyTaskEvent(manager).Parse(client, HabbiconTestSupport.Incoming(taskId));
        Assert.Equal(0, habbo.Duckets);
        Assert.Equal(0, claims);

        manager.Progress(client, "chat");
        sent.Clear();

        await new ClaimDailyTaskEvent(manager).Parse(client, HabbiconTestSupport.Incoming(taskId));
        Assert.Equal([ServerPacketHeader.DailyTaskUpdatedComposer, ServerPacketHeader.HabboActivityPointNotificationComposer], sent.Select(packet => (uint)packet.Header));
        Assert.Equal(25, habbo.Duckets);
        Assert.Equal(DailyTaskStatus.Claimed, store.Row(taskId).Status);
        Assert.Equal(1, claims);
        sent.Clear();

        await new ClaimDailyTaskEvent(manager).Parse(client, HabbiconTestSupport.Incoming(taskId));
        Assert.Equal(25, habbo.Duckets);
        Assert.Equal(1, claims);
        Assert.Equal(1, store.Claims);
    }

    [Fact]
    public void AFailedClaimLeavesTheTaskClaimable()
    {
        var (manager, store, _) = Manager(regular: [Definition(1, "chat")]);
        var habbo = User();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        manager.SendActive(client);
        var taskId = (int)ActiveTasks(sent)[0].TaskId;
        manager.Progress(client, "chat");
        store.FailClaims = true;
        sent.Clear();

        manager.Claim(client, taskId);

        Assert.Equal(ServerPacketHeader.DailyTaskUpdatedComposer, Assert.Single(sent).Header);
        Assert.Equal(DailyTaskStatus.Completed, store.Row(taskId).Status);
        Assert.Equal(0, habbo.Duckets);

        store.FailClaims = false;
        manager.Claim(client, taskId);
        Assert.Equal(25, habbo.Duckets);
    }

    [Fact]
    public void ANewDayBringsNewTasksAndKeepsCompletedOnesClaimable()
    {
        var (manager, store, clock) = Manager(regular: [Definition(1, "chat"), Definition(2, "wave")]);
        var habbo = User();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        manager.SendActive(client);
        manager.Progress(client, "chat");
        var completed = store.Rows.Single(row => row.DefinitionId == 1).Id;

        clock.Now = Now.AddHours(3);
        sent.Clear();
        manager.Progress(client, "wave");

        // The new day's list first, then the progress on the new day's wave task.
        Assert.Equal(ServerPacketHeader.ActiveDailyTasksComposer, sent[0].Header);
        var tasks = ActiveTasks(sent);
        Assert.Equal(3, tasks.Count);
        var old = Assert.Single(tasks, task => task.TaskId == completed);
        Assert.Equal((DailyTaskStatus.Completed, -1), (old.Status, old.SecondsLeft));
        Assert.Equal(DailyTaskStatus.InProgress, store.Rows.Single(row => row.Day == Today && row.DefinitionId == 2).Status);
        Assert.Equal(DailyTaskStatus.Completed, store.Rows.Single(row => row.Day == Today.AddDays(1) && row.DefinitionId == 2).Status);

        manager.Claim(client, (int)completed);
        Assert.Equal(25, habbo.Duckets);
    }

    [Fact]
    public void TheResetSendsEveryoneOnlineTheNewList()
    {
        var clients = new List<GameClient>();
        var (manager, _, clock) = Manager(clients: clients);
        var (client, sent) = HabbiconTestSupport.Client(User());
        clients.Add(client);
        manager.SendActive(client);
        clock.Now = Now.AddHours(2);
        sent.Clear();

        manager.RefreshOnline();

        Assert.Equal(3, ActiveTasks(sent).Count);
        Assert.All(ActiveTasks(sent), task => Assert.Equal(86400, task.SecondsLeft));
    }

    [Fact]
    public void TheRequestAnswersWithTheList()
    {
        var (manager, _, _) = Manager();
        var (client, sent) = HabbiconTestSupport.Client(User());

        new GetDailyTasksEvent(manager).Parse(client, HabbiconTestSupport.Incoming());

        Assert.Equal(ServerPacketHeader.ActiveDailyTasksComposer, Assert.Single(sent).Header);
    }

    [Fact]
    public void RewardTrackProgressReachesDailyTasksAndAClaimIsATrackAction()
    {
        var daily = new RecordingDailyTasks();
        var tracks = new RewardTrackManager(NullLogger<RewardTrackManager>.Instance, null!, null!, new Clock(Now, Zone), daily);
        var (client, _) = HabbiconTestSupport.Client(User());

        tracks.Progress(client, RewardTrackActions.Wave);
        tracks.Progress(client, RewardTrackActions.FindHandItem, 1, "8");
        daily.RaiseClaimed(client);

        Assert.Equal([(RewardTrackActions.Wave, 1, (string?)null), (RewardTrackActions.FindHandItem, 1, "8"), (RewardTrackActions.ClaimDailyTask, 1, null)], daily.Calls);
    }

    [Fact]
    public void TheProductionScanHandsTheDailyTasksToTheRewardTracks()
    {
        var services = new ServiceCollection();
        services.AddSingleton(EditorTestSupport.UntouchableDatabase());
        services.AddSingleton<ISettingsManager>(new Settings());
        services.AddSingleton<IGameClientManager>(ClientList.Create([]));
        services.AddSingleton<TimeProvider>(new Clock(Now, Zone));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        Plus.Program.AddAssignableTo<IStartable>(services, typeof(Plus.Program).Assembly);
        Plus.Program.AddAssignableTo<IDailyTaskStore>(services, typeof(Plus.Program).Assembly);
        using var provider = services.BuildServiceProvider();

        var daily = provider.GetRequiredService<IDailyTaskManager>();
        var tracks = provider.GetRequiredService<IRewardTrackManager>();

        Assert.Same(daily, provider.GetRequiredService<DailyTaskManager>());
        Assert.Same(daily, typeof(RewardTrackManager).GetField("_dailyTasks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tracks));
    }

    [Fact]
    public void TheMigrationAndTheFreshInstallSeedTheSameTasks()
    {
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/97_DailyTasks.sql"));
        var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
        var rows = migration.Split('\n').Where(line => line.StartsWith('(')).Select(line => line.TrimEnd(',', ';', '\r')).ToList();

        Assert.Equal(36, rows.Count);
        Assert.All(rows, row => Assert.Contains(row, fresh));
        Assert.Contains("CREATE TABLE `user_daily_tasks`", fresh);
        Assert.DoesNotContain("START TRANSACTION", migration, StringComparison.OrdinalIgnoreCase);
    }

    [DailyTaskDatabaseFact]
    public void TheStoreKeepsOneClaimPerTaskAndPaysWithIt()
    {
        using var root = new MySqlConnection(Environment.GetEnvironmentVariable("DAILY_TASKS_DATABASE"));
        root.Open();
        var schema = $"daily_tasks_{Guid.NewGuid():N}";
        root.Execute($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            var cs = new MySqlConnectionStringBuilder(root.ConnectionString) { Database = schema }.ConnectionString;
            using var connection = new MySqlConnection(cs);
            connection.Open();
            connection.Execute("""
                CREATE TABLE users (id INT(11) NOT NULL AUTO_INCREMENT PRIMARY KEY, credits INT NOT NULL DEFAULT 0) ENGINE=InnoDB;
                CREATE TABLE user_currencies (user_id INT(11) NOT NULL, type INT(11) NOT NULL, amount INT(11) NOT NULL DEFAULT 0, PRIMARY KEY (user_id, type)) ENGINE=InnoDB;
                CREATE TABLE user_badges (id INT(11) NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT(11) NOT NULL, badge_id VARCHAR(100) NOT NULL,
                    badge_slot INT(11) NOT NULL DEFAULT 0, UNIQUE KEY user_badge (user_id, badge_id)) ENGINE=InnoDB;
                INSERT INTO users (id, credits) VALUES (7, 10);
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/97_DailyTasks.sql"));
            connection.Execute(migration);
            connection.Execute(migration);

            var store = new DailyTaskStore(new RawDatabase(cs), NullLogger<DailyTaskStore>.Instance);
            var definitions = store.LoadDefinitions();
            Assert.Equal(18, definitions.Count);
            Assert.Single(definitions, definition => definition.Bonus);
            Assert.Equal(new[] { "1", "8", "10", "11", "12", "13", "14", "15", "16", "17", "27", "53", "91", "146", "149" }.ToHashSet(),
                definitions.Single(definition => definition.Code == "1742193348839_G").Parameters.ToHashSet());
            Assert.All(definitions, definition => Assert.NotNull(DailyTaskRules.Grant(definition, false)));

            var expires = DailyTaskRules.ResetAfter(Today, Zone);
            var rows = store.Assign(7, Today, expires, [1, 2]);
            Assert.Equal(2, rows.Count);
            Assert.Equal(rows.Select(row => row.Id), store.Assign(7, Today, expires, [1, 2]).Select(row => row.Id));
            Assert.Equal(expires, rows[0].ExpiresAt);
            Assert.Equal(Today, rows[0].Day);

            Assert.True(store.SaveProgress(7, rows[0].Id, 10, DailyTaskStatus.Completed, Now));
            Assert.False(store.SaveProgress(7, rows[0].Id, 10, DailyTaskStatus.Completed, Now));
            var grant = new DailyTaskGrant(3, ImmutableSortedDictionary.CreateRange([new KeyValuePair<int, int>(0, 25)]), ["ACH_Test1"]);
            Assert.Equal(DailyTaskClaimOutcome.NotClaimable, store.Claim(7, rows[1].Id, grant, Now));
            Assert.Equal(DailyTaskClaimOutcome.Claimed, store.Claim(7, rows[0].Id, grant, Now));
            Assert.Equal(DailyTaskClaimOutcome.NotClaimable, store.Claim(7, rows[0].Id, grant, Now));
            Assert.Equal(13, connection.ExecuteScalar<int>("SELECT credits FROM users WHERE id = 7"));
            Assert.Equal(25, connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id = 7 AND type = 0"));
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_badges WHERE user_id = 7 AND badge_id = 'ACH_Test1'"));

            // Tomorrow: today's completed-and-claimed task is gone, a completed unclaimed one stays listed.
            Assert.True(store.SaveProgress(7, rows[1].Id, 5, DailyTaskStatus.Completed, Now));
            var tomorrow = store.LoadUser(7, Today.AddDays(1), Today.AddDays(-6));
            Assert.Equal([rows[1].Id], tomorrow.Select(row => row.Id));
            Assert.Equal((5, DailyTaskStatus.Completed), (tomorrow[0].Progress, tomorrow[0].Status));
            Assert.Empty(store.LoadUser(7, Today.AddDays(8), Today.AddDays(1)));
        }
        finally {
            root.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static List<DailyTaskWire> ActiveTasks(List<(uint Header, byte[] Payload)> sent)
    {
        var payload = sent.Last(packet => packet.Header == ServerPacketHeader.ActiveDailyTasksComposer).Payload;
        var reader = new WireReader(payload);
        var count = reader.Int();
        var tasks = new List<DailyTaskWire>();

        for (var i = 0; i < count; i++) {
            var id = ((long)reader.Int() << 32) | (uint)reader.Int();
            var code = reader.String();
            var questType = reader.String();
            var bonus = reader.Byte() == 1;
            var imageVersion = reader.String();
            var catalog = reader.String();
            var required = reader.Int();
            var repeats = reader.Int();
            var status = (DailyTaskStatus)reader.Byte();
            var secondsLeft = reader.Int();
            var rewards = ImmutableArray.CreateBuilder<DailyTaskReward>();

            for (var rewardCount = reader.Int(); rewardCount > 0; rewardCount--) {
                rewards.Add(new(reader.Short(), reader.String(), reader.String(), reader.Int()));
            }

            tasks.Add(new(id, code, questType, bonus, imageVersion, catalog, required, repeats, status, secondsLeft, rewards.ToImmutable()));
        }

        Assert.Equal(payload.Length, reader.Offset);

        return tasks;
    }

    private static DailyTaskDefinition Definition(int id, string action, int repeats = 1, string parameters = "", bool bonus = false, int weight = 1,
        ImmutableArray<DailyTaskReward>? rewards = null) =>
        new(id, $"task{id}_G", action, DailyTaskDefinition.ParseParameters(parameters), action, repeats, "", "", bonus, weight,
            rewards ?? [new DailyTaskReward(DailyTaskReward.Currency, "0", "", 25)]);

    private static (DailyTaskManager Manager, FakeStore Store, Clock Clock) Manager(IReadOnlyList<DailyTaskDefinition>? regular = null,
        List<GameClient>? clients = null)
    {
        var definitions = (regular ?? [Definition(1, "chat"), Definition(2, "wave"), Definition(3, "dance"), Definition(4, "swim")])
            .Append(Definition(9, "show_sign", parameters: "10", bonus: true)).ToList();
        var store = new FakeStore(definitions);
        var clock = new Clock(Now, Zone);
        var manager = new DailyTaskManager(store, new Settings(), ClientList.Create(clients ?? []), new NoBadges(), clock,
            NullLogger<DailyTaskManager>.Instance);
        manager.Load();

        return (manager, store, clock);
    }

    private static Habbo User() => new()
    {
        Id = 7,
        Username = "johndoe",
        Access = UserAccess.Empty,
        Inventory = new InventoryComponent { Badges = new([]), Furniture = new([], []) }
    };

    private sealed class WireReader(byte[] payload)
    {
        public int Offset { get; private set; }

        public int Int()
        {
            var value = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(Offset, 4));
            Offset += 4;

            return value;
        }

        public short Short()
        {
            var value = System.Buffers.Binary.BinaryPrimitives.ReadInt16BigEndian(payload.AsSpan(Offset, 2));
            Offset += 2;

            return value;
        }

        public byte Byte() => payload[Offset++];

        public string String()
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(Offset, 2));
            var value = System.Text.Encoding.UTF8.GetString(payload, Offset + 2, length);
            Offset += 2 + length;

            return value;
        }
    }

    internal sealed class FakeStore(IReadOnlyList<DailyTaskDefinition> definitions) : IDailyTaskStore
    {
        private long _nextId = 1;
        public List<UserDailyTask> Rows { get; } = [];
        public bool FailClaims { get; set; }
        public int Claims { get; private set; }

        public UserDailyTask Row(long id) => Rows.Single(row => row.Id == id);

        public UserDailyTask Definition(int definitionId) => Rows.Single(row => row.DefinitionId == definitionId);

        public IReadOnlyList<DailyTaskDefinition> LoadDefinitions() => definitions;

        // Copies, as the database hands out fresh rows.
        public IReadOnlyList<UserDailyTask> LoadUser(int userId, DateOnly today, DateOnly unclaimedSince) => Rows
            .Where(row => row.Day == today || row.Status == DailyTaskStatus.Completed && row.Day >= unclaimedSince && row.Day < today)
            .Select(Copy).ToList();

        public IReadOnlyList<UserDailyTask> Assign(int userId, DateOnly day, DateTimeOffset expiresAt, IReadOnlyList<int> definitionIds)
        {
            foreach (var id in definitionIds.Where(id => !Rows.Any(row => row.Day == day && row.DefinitionId == id))) {
                Rows.Add(new(_nextId++, id, day, expiresAt, 0, DailyTaskStatus.InProgress));
            }

            return Rows.Where(row => row.Day == day && definitionIds.Contains(row.DefinitionId)).Select(Copy).ToList();
        }

        public bool SaveProgress(int userId, long taskId, int progress, DailyTaskStatus status, DateTimeOffset now)
        {
            var row = Row(taskId);

            if (row.Status != DailyTaskStatus.InProgress) {
                return false;
            }

            row.Progress = progress;
            row.Status = status;

            return true;
        }

        public DailyTaskClaimOutcome Claim(int userId, long taskId, DailyTaskGrant grant, DateTimeOffset now)
        {
            if (FailClaims) {
                return DailyTaskClaimOutcome.Failed;
            }

            var row = Row(taskId);

            if (row.Status != DailyTaskStatus.Completed) {
                return DailyTaskClaimOutcome.NotClaimable;
            }

            row.Status = DailyTaskStatus.Claimed;
            Claims++;

            return DailyTaskClaimOutcome.Claimed;
        }

        private static UserDailyTask Copy(UserDailyTask row) => new(row.Id, row.DefinitionId, row.Day, row.ExpiresAt, row.Progress, row.Status);
    }

    internal sealed class Clock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => zone;
    }

    private sealed class Settings : ISettingsManager
    {
        public string TryGetValue(string value) => "0";
        public string? GetOptionalValue(string key) => null;
        public Task Reload() => Task.CompletedTask;
    }

    private sealed class NoBadges : IBadgeManager
    {
        public IReadOnlyDictionary<string, BadgeDefinition> Badges { get; } = new Dictionary<string, BadgeDefinition>();
        public Task Init() => Task.CompletedTask;
        public Task GiveBadge(Habbo habbo, string code) => Task.CompletedTask;
        public Task RemoveBadge(Habbo habbo, string badge) => Task.CompletedTask;
        public Task<bool> ReplaceBadge(Habbo habbo, string? previous, string code) => Task.FromResult(false);
        public Task<List<Badge>> LoadBadgesForHabbo(int userId) => Task.FromResult(new List<Badge>());
    }

    public class ClientList : DispatchProxy
    {
        private List<GameClient> _clients = [];

        public static IGameClientManager Create(List<GameClient> clients)
        {
            var manager = DispatchProxy.Create<IGameClientManager, ClientList>();
            ((ClientList)(object)manager)._clients = clients;

            return manager;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "get_" + nameof(IGameClientManager.GetClients) ? _clients : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class RecordingDailyTasks : IDailyTaskManager
    {
        public List<(string Action, int Amount, string? Parameter)> Calls { get; } = [];
        public event Action<GameClient>? TaskClaimed;
        public void RaiseClaimed(GameClient session) => TaskClaimed?.Invoke(session);
        public void SendActive(GameClient session) => throw new NotSupportedException();
        public void Progress(GameClient session, string actionType, int amount = 1, string? parameter = null) => Calls.Add((actionType, amount, parameter));
        public void Claim(GameClient session, int taskId) => throw new NotSupportedException();
    }

    private sealed class RawDatabase(string connectionString) : IDatabase
    {
        public IDbConnection Connection() => new MySqlConnection(connectionString);
        public bool IsConnected() => true;
    }
}

public sealed class DailyTaskDatabaseFactAttribute : FactAttribute
{
    public DailyTaskDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DAILY_TASKS_DATABASE"))) {
            Skip = "Set DAILY_TASKS_DATABASE to an isolated MariaDB server with CREATE DATABASE permission.";
        }
    }
}
