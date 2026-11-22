using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.Database;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class QuestProgressStoreDatabaseTests
{
    private const int UserId = 7;
    private static readonly Quest RequestFriend = new(115, "social", 3, QuestType.SocialFriend, 1, "REQUESTFRIEND", 10, "", 0, null, null);
    private static readonly Quest MoveItem = new(101, "room_builder", 1, QuestType.FurniMove, 3, "MOVEITEM", 10, "", 0, null, null);

    [RoomComponentDatabaseFact]
    public void StartRefusesACompletedQuestAndKeepsItsSavedRow() => WithSchema((database, connection) =>
    {
        connection.Execute("INSERT INTO user_quests (user_id, quest_id, progress) VALUES (@UserId, 115, 1)", new { UserId });

        var started = new QuestProgressStore(database).TryStart(UserId, RequestFriend, out var progress);

        Assert.False(started);
        Assert.Equal(1, progress);
        Assert.Equal(1, SavedProgress(connection, RequestFriend.Id));
        Assert.Equal(0, ActiveQuest(connection));
    });

    [RoomComponentDatabaseFact]
    public void StartKeepsTheSavedProgressOfAQuestInProgress() => WithSchema((database, connection) =>
    {
        connection.Execute("INSERT INTO user_quests (user_id, quest_id, progress) VALUES (@UserId, 101, 2)", new { UserId });

        var started = new QuestProgressStore(database).TryStart(UserId, MoveItem, out var progress);

        Assert.True(started);
        Assert.Equal(2, progress);
        Assert.Equal(2, SavedProgress(connection, MoveItem.Id));
        Assert.Equal(MoveItem.Id, ActiveQuest(connection));
    });

    [RoomComponentDatabaseFact]
    public void StartCreatesAnEmptyRowForANewQuest() => WithSchema((database, connection) =>
    {
        var started = new QuestProgressStore(database).TryStart(UserId, MoveItem, out var progress);

        Assert.True(started);
        Assert.Equal(0, progress);
        Assert.Equal(0, SavedProgress(connection, MoveItem.Id));
        Assert.Equal(MoveItem.Id, ActiveQuest(connection));
    });

    [RoomComponentDatabaseFact]
    public void CompletedQuestPaysItsRewardOnlyOnceInTheSameSessionAndAfterARelog() => WithSchema((database, connection) =>
    {
        connection.Execute("""
            INSERT INTO quests VALUES (115, 'social', 3, 10, 1, 'REQUESTFRIEND', 10, '', 0, NULL, NULL)
            """);
        var store = new QuestProgressStore(database);
        var manager = new QuestManager(database, CatalogSnapshotTestSupport.Proxy<IMessengerDataLoader>((_, _) => null),
            TestLogging.For<QuestManager>(), store);
        manager.Init();
        var service = new QuestProgressService(store, manager);

        var (first, _) = Login();
        service.Start(first, RequestFriend.Id);
        manager.ProgressUserQuest(first, QuestType.SocialFriend);
        Assert.Equal(10, first.GetHabbo().Duckets);

        service.Start(first, RequestFriend.Id);
        manager.ProgressUserQuest(first, QuestType.SocialFriend);
        Assert.Equal(10, first.GetHabbo().Duckets);

        var (relogged, sent) = Login();
        service.Start(relogged, RequestFriend.Id);
        manager.ProgressUserQuest(relogged, QuestType.SocialFriend);

        Assert.Equal(0, relogged.GetHabbo().Duckets);
        Assert.Equal(0, relogged.GetHabbo().HabboStats!.QuestId);
        Assert.DoesNotContain(sent, packet => packet.Header == ServerPacketHeader.QuestStartedComposer);
        Assert.Equal(1, SavedProgress(connection, RequestFriend.Id));
        Assert.Equal(0, ActiveQuest(connection));
    });

    [RoomComponentDatabaseFact]
    public void LoginLoadsSavedQuestProgressSoTheListAndTheNextStepContinueFromIt() => WithSchema((database, connection) =>
    {
        connection.Execute("""
            INSERT INTO quests VALUES (115, 'social', 1, 10, 1, 'REQUESTFRIEND', 10, '', 0, NULL, NULL),
                (116, 'social', 2, 9, 3, 'CHAT', 10, '', 0, NULL, NULL),
                (101, 'room_builder', 1, 0, 5, 'MOVEITEM', 10, '', 0, NULL, NULL);
            INSERT INTO user_quests (user_id, quest_id, progress) VALUES (7, 115, 1), (7, 101, 2);
            UPDATE user_statistics SET quest_id = 101 WHERE id = 7;
            """);
        var store = new QuestProgressStore(database);
        var manager = new QuestManager(database, CatalogSnapshotTestSupport.Proxy<IMessengerDataLoader>((_, _) => null),
            TestLogging.For<QuestManager>(), store);
        manager.Init();
        var (client, sent) = Login(activeQuest: 101);

        new LoadUserQuestsTask(database).Load(client.GetHabbo()).GetAwaiter().GetResult();
        manager.GetList(client, null);

        var list = ListedQuests(sent.Last(packet => packet.Header == ServerPacketHeader.QuestListComposer).Payload);
        Assert.Equal((116, false, 0), list["social"]);
        Assert.Equal((101, true, 2), list["room_builder"]);

        manager.ProgressUserQuest(client, QuestType.FurniMove);

        Assert.Equal(3, SavedProgress(connection, 101));
        Assert.Equal(0, client.GetHabbo().Duckets);
    });

    private static Dictionary<string, (int Id, bool Started, int Progress)> ListedQuests(byte[] payload)
    {
        var reader = new FlashIncomingPacket { Buffer = payload };
        var quests = new Dictionary<string, (int, bool, int)>();

        for (var count = reader.ReadInt(); count > 0; count--) {
            var category = reader.ReadString();
            reader.ReadInt();
            reader.ReadInt();
            reader.ReadInt();
            var id = reader.ReadInt();
            var started = reader.ReadBool();
            reader.ReadString();
            reader.ReadString();
            reader.ReadInt();
            reader.ReadString();
            var progress = reader.ReadInt();
            reader.ReadInt();
            reader.ReadInt();
            reader.ReadString();
            reader.ReadString();
            reader.ReadBool();
            reader.ReadBool();
            quests[category] = (id, started, progress);
        }

        return quests;
    }

    private static (GameClient Client, List<(uint Header, byte[] Payload)> Sent) Login(int activeQuest = 0)
    {
        var stats = new HabboStats(0, 0, 0, 0, 0, 0, 0, activeQuest, 0, "");

        return HabbiconTestSupport.Client(new Habbo { Id = UserId, HabboStats = stats });
    }

    private static int SavedProgress(MySqlConnection connection, int questId) =>
        connection.QuerySingle<int>("SELECT progress FROM user_quests WHERE user_id = @UserId AND quest_id = @questId", new { UserId, questId });

    private static int ActiveQuest(MySqlConnection connection) =>
        connection.QuerySingle<int>("SELECT quest_id FROM user_statistics WHERE id = @UserId", new { UserId });

    private static void WithSchema(Action<IDatabase, MySqlConnection> test)
    {
        var server = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var connection = new MySqlConnection(server);
        connection.Open();
        var schema = "quest_progress_" + Guid.NewGuid().ToString("N");
        connection.Execute($"CREATE DATABASE `{schema}`");

        try {
            connection.Execute($"USE `{schema}`");
            connection.Execute("""
                CREATE TABLE quests (
                    id INT UNSIGNED PRIMARY KEY, type VARCHAR(32) NOT NULL, level_num INT NOT NULL,
                    goal_type INT NOT NULL, goal_data INT UNSIGNED NOT NULL, action VARCHAR(32) NOT NULL,
                    pixel_reward INT NOT NULL, data_bit VARCHAR(2) NOT NULL, reward_type INT NOT NULL,
                    timestamp_unlock DATETIME(6) NULL, timestamp_lock DATETIME(6) NULL);
                CREATE TABLE user_quests (
                    user_id INT NOT NULL, quest_id INT UNSIGNED NOT NULL, progress INT DEFAULT 0,
                    PRIMARY KEY (user_id, quest_id));
                CREATE TABLE user_statistics (id INT PRIMARY KEY, quest_id INT UNSIGNED NOT NULL DEFAULT 0);
                INSERT INTO user_statistics (id) VALUES (7);
                """);

            test(new SchemaDatabase(new MySqlConnectionStringBuilder(server) { Database = schema }.ConnectionString), connection);
        }
        finally {
            connection.Execute("USE information_schema");
            connection.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class SchemaDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
