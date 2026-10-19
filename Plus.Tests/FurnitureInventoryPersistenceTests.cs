using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using System.Runtime.CompilerServices;
using Dapper;
using MySqlConnector;
using Plus.Communication.Packets.Outgoing;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed class FurnitureInventoryPersistenceTests
{
    [Fact]
    public void ClearPublishesOnlyAfterPersistence()
    {
        var context = Context();
        var store = new RecordingStore(() =>
        {
            context.Events.Add("store");
            Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
            Assert.Empty(context.Sent);
        });

        Assert.True(new InventoryClearService(store, context.Gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room));

        Assert.Equal(new[] { "gate-enter", "store", "gate-exit" }, context.Events);
        Assert.Null(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
        Assert.Equal(new uint[] { ServerPacketHeader.FurniListUpdateComposer, ServerPacketHeader.BroadcastMessageAlertComposer },
            context.Sent.Select(packet => packet.Header));
        Assert.Empty(context.Sent[0].Payload);
        var notification = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = context.Sent[1].Payload };
        Assert.Equal("Your inventory has been cleared!", notification.ReadString());
        Assert.Equal("", notification.ReadString());
    }

    [Fact]
    public void StoreFailureLeavesInventoryAndPacketsUntouched()
    {
        var context = Context();
        var store = new RecordingStore(() => context.Events.Add("store")) { Failure = new InvalidOperationException("forced") };

        Assert.Throws<InvalidOperationException>(() => new InventoryClearService(store, context.Gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room));

        Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
        Assert.Empty(context.Sent);
        Assert.Equal(new[] { "gate-enter", "store", "gate-exit" }, context.Events);
    }

    [Fact]
    public void ClearAddsRefundedChestCoinsToTheWalletAndExplainsWhy()
    {
        var context = Context();
        context.Habbo.Credits = 100;
        var store = new RecordingStore { Result = new([7], [], 600, 500, 2, 0) };

        Assert.True(new InventoryClearService(store, context.Gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room));

        Assert.Equal(100, store.LiveCredits);
        Assert.Equal(600, context.Habbo.Credits);
        Assert.Null(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
        Assert.Equal(new uint[] { ServerPacketHeader.FurniListUpdateComposer, ServerPacketHeader.CreditBalanceComposer,
            ServerPacketHeader.BroadcastMessageAlertComposer, ServerPacketHeader.BroadcastMessageAlertComposer }, context.Sent.Select(packet => packet.Header));
        Assert.Equal("600.0", new Plus.Communication.Flash.FlashIncomingPacket { Buffer = context.Sent[1].Payload }.ReadString());
        Assert.Equal("Your inventory has been cleared!", Alert(context.Sent[2].Payload));
        Assert.Equal("500 credits were added to your wallet.\n\nThey were the coins inside the 2 chests that were deleted from your inventory.",
            Alert(context.Sent[3].Payload));
    }

    [Fact]
    public void ClearKeepsChestsThatWouldOverflowTheWalletAndExplainsWhy()
    {
        var context = Context();
        context.Habbo.Credits = int.MaxValue;
        var chest = new InventoryItem { Id = 8, OwnerId = 1, Definition = new ItemDefinition { Id = 100, Type = ItemType.Floor } };
        Assert.True(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.AddItem(chest));
        var store = new RecordingStore { Result = new([7], [], int.MaxValue, 0, 0, 1) };

        Assert.True(new InventoryClearService(store, context.Gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room));

        var furniture = Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture;
        Assert.Null(furniture.GetItem(7));
        Assert.Same(chest, furniture.GetItem(8));
        Assert.Equal(int.MaxValue, context.Habbo.Credits);
        Assert.Equal(new uint[] { ServerPacketHeader.FurniListUpdateComposer, ServerPacketHeader.BroadcastMessageAlertComposer,
            ServerPacketHeader.BroadcastMessageAlertComposer }, context.Sent.Select(packet => packet.Header));
        Assert.Equal("Your inventory has been cleared!", Alert(context.Sent[1].Payload));
        Assert.Equal("1 chest was kept in your inventory.\n\nThe coins inside it would take your credits above the maximum of 2147483647, " +
            "so they could not be added to your wallet. Spend some credits or take coins out of the chest, then type :emptyitems yes again.",
            Alert(context.Sent[2].Payload));
    }

    private static string Alert(byte[] payload) => new Plus.Communication.Flash.FlashIncomingPacket { Buffer = payload }.ReadString();

    [Fact]
    public void ActiveTradeIsRefusedWithoutPersistenceOrPublication()
    {
        var context = Context();
        context.RoomUser.IsTrading = true;
        var store = new RecordingStore();

        Assert.False(new InventoryClearService(store, context.Gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room));

        Assert.Equal(0, store.Calls);
        Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
        Assert.Empty(context.Sent);
    }

    [Fact]
    public void StaleOrClosedSessionIsRefusedWithoutPersistence()
    {
        var stale = Context();
        stale.Habbo.Client = null!;
        var staleStore = new RecordingStore();
        Assert.False(new InventoryClearService(staleStore, stale.Gate, TestGameClientManager.Empty).TryClear(stale.Client, stale.Room));
        Assert.Equal(0, staleStore.Calls);

        var closed = Context();
        typeof(Habbo).GetField("_disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(closed.Habbo, true);
        var closedStore = new RecordingStore();
        Assert.False(new InventoryClearService(closedStore, closed.Gate, TestGameClientManager.Empty).TryClear(closed.Client, closed.Room));
        Assert.Equal(0, closedStore.Calls);

        var wrongRoom = Context();
        wrongRoom.Habbo.CurrentRoom = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var wrongRoomStore = new RecordingStore();
        Assert.False(new InventoryClearService(wrongRoomStore, wrongRoom.Gate, TestGameClientManager.Empty).TryClear(wrongRoom.Client, wrongRoom.Room));
        Assert.Equal(0, wrongRoomStore.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConfirmationControlsTheClearWorkflow(bool confirmed)
    {
        var inventory = new RecordingClearService();
        var command = new Plus.HabboHotel.Rooms.Chat.Commands.User.EmptyItems(inventory);
        var context = Context();

        command.Execute(context.Client, context.Room, confirmed ? ["yes"] : []);

        Assert.Equal(confirmed ? 1 : 0, inventory.Calls);
    }

    [Fact]
    public void WrongConfirmationPreservesTheExistingNotice()
    {
        var inventory = new RecordingClearService();
        var command = new Plus.HabboHotel.Rooms.Chat.Commands.User.EmptyItems(inventory);
        var context = Context();

        command.Execute(context.Client, context.Room, ["no"]);

        Assert.Equal(0, inventory.Calls);
        var notification = Assert.Single(context.Sent);
        Assert.Equal(ServerPacketHeader.BroadcastMessageAlertComposer, notification.Header);
        var packet = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = notification.Payload };
        Assert.Equal("To confirm, you must type in :emptyitems yes", packet.ReadString());
        Assert.Equal("", packet.ReadString());
    }

    [Fact]
    public async Task RealAccountGateHoldsPersistenceAndPublicationUntilRelease()
    {
        var context = Context();
        var gate = new AccountSessionGate();
        var store = new RecordingStore();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var held = gate.Enter(context.Habbo.Id);
        Task<bool>? worker = null;

        try {
            worker = Task.Run(() =>
            {
                started.TrySetResult();

                return new InventoryClearService(store, gate, TestGameClientManager.Empty).TryClear(context.Client, context.Room);
            });
            await started.Task;
            await Assert.ThrowsAsync<TimeoutException>(() => worker.WaitAsync(TimeSpan.FromMilliseconds(100)));
            Assert.Equal(0, store.Calls);
            Assert.NotNull(Assert.IsType<InventoryComponent>(context.Habbo.Inventory).Furniture.GetItem(7));
            Assert.Empty(context.Sent);
        }
        finally {
            held.Dispose();

            if (worker != null) {
                try {
                    await worker.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch { /* Preserve an assertion already escaping the try; the success path observes below. */ }
            }
        }

        Assert.True(await worker!.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, store.Calls);
        Assert.Null(context.Habbo.Inventory.Furniture.GetItem(7));
        Assert.Equal(2, context.Sent.Count);
    }

    [RoomComponentDatabaseFact]
    public async Task LoaderAndClearStorePreserveOwnerRoomPredicatesAndRollback()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "task_refactor_tests_inventory_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(root)
        {
            Database = "",
            Pooling = false,
            AllowZeroDateTime = true,
            ConvertZeroDateTime = true
        };
        await using var server = new MySqlConnection(builder.ConnectionString);
        await server.OpenAsync();
        await server.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4");

        try {
            builder.Database = schema;
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("""
                CREATE TABLE users (id INT PRIMARY KEY);
                CREATE TABLE items (id INT UNSIGNED PRIMARY KEY, user_id INT NOT NULL, room_id INT UNSIGNED NOT NULL,
                    base_item INT UNSIGNED NOT NULL, extra_data TEXT NOT NULL, limited_number INT UNSIGNED NOT NULL,
                    limited_stack INT UNSIGNED NOT NULL);
                CREATE TABLE items_groups (id INT UNSIGNED PRIMARY KEY, group_id INT NOT NULL);
                CREATE TABLE items_builders_club (item_id INT UNSIGNED PRIMARY KEY, user_id INT NOT NULL);
                CREATE TABLE wired_chests (item_id INT UNSIGNED PRIMARY KEY, coins INT UNSIGNED NOT NULL DEFAULT 0);
                INSERT INTO users VALUES (1),(2);
                INSERT INTO items VALUES
                    (10,1,0,100,'legacy-a',4,40),(11,1,0,999,'unknown',5,50),(12,1,0,101,'legacy-b',6,60),
                    (13,1,9,100,'room',7,70),(14,2,0,100,'other',8,80);
                INSERT INTO items_groups VALUES (10,3);
                """);
            await connection.ExecuteAsync(RoomMusicDatabaseTests.Fixture.SchemaSql);
            var definitions = new Definitions(
                new ItemDefinition { Id = 100, Type = ItemType.Floor },
                new ItemDefinition { Id = 101, Type = ItemType.Wall },
                new ItemDefinition { Id = 102, Type = ItemType.Wall, InteractionType = InteractionType.CameraPicture },
                new ItemDefinition { Id = 103, Type = ItemType.Wall, InteractionType = InteractionType.Floor });
            var database = new TestDatabase(builder.ConnectionString);

            var loaded = await new FurnitureInventoryLoader(database, definitions).Load(1);

            Assert.Equal(new uint[] { 10, 12 }, loaded.Select(item => item.Id));
            Assert.Equal(new uint[] { 4, 6 }, loaded.Select(item => item.UniqueNumber));
            Assert.Equal(new uint[] { 40, 60 }, loaded.Select(item => item.UniqueSeries));
            Assert.All(loaded, item => Assert.Equal("", item.ExtraData.Serialize()));

            var store = new InventoryClearStore(database, definitions);
            await connection.ExecuteAsync("""
                CREATE TRIGGER fail_inventory_clear BEFORE DELETE ON items FOR EACH ROW
                BEGIN
                    IF OLD.user_id=1 AND OLD.room_id=0 AND OLD.id=12 THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced inventory clear failure';
                    END IF;
                END
                """);
            var failedClear = Context(1, 10);
            await Assert.ThrowsAsync<MySqlException>(() => Task.Run(() =>
                new InventoryClearService(store, failedClear.Gate, TestGameClientManager.Empty).TryClear(failedClear.Client, failedClear.Room)));
            Assert.Equal(new uint[] { 10, 11, 12 }, await connection.QueryAsync<uint>(
                "SELECT id FROM items WHERE user_id=1 AND room_id=0 ORDER BY id"));
            Assert.NotNull(Assert.IsType<InventoryComponent>(failedClear.Habbo.Inventory).Furniture.GetItem(10));
            Assert.Empty(failedClear.Sent);
            await connection.ExecuteAsync("DROP TRIGGER fail_inventory_clear");

            store.DeleteAll(1, 0);
            Assert.Equal(new uint[] { 13, 14 }, await connection.QueryAsync<uint>("SELECT id FROM items ORDER BY id"));
            store.DeleteAll(1, 0);
            Assert.Equal(new uint[] { 13, 14 }, await connection.QueryAsync<uint>("SELECT id FROM items ORDER BY id"));

            await connection.ExecuteAsync("DELETE FROM users WHERE id=2");
            var missingOwner = Context(2, 14);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() =>
                new InventoryClearService(store, missingOwner.Gate, TestGameClientManager.Empty).TryClear(missingOwner.Client, missingOwner.Room)));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM items WHERE id=14 AND user_id=2 AND room_id=0"));
            Assert.NotNull(Assert.IsType<InventoryComponent>(missingOwner.Habbo.Inventory).Furniture.GetItem(14));
            Assert.Empty(missingOwner.Sent);

            // A photo's image record must survive a relog, or it is placed without its image.
            const string photo = "{\"w\":\"/camera/photo.png\"}";
            await connection.ExecuteAsync("INSERT INTO items VALUES (15,1,0,102,@photo,0,0)", new { photo });
            Assert.Equal(photo, Assert.Single(await new FurnitureInventoryLoader(database, definitions).Load(1)).ExtraData.Serialize());

            // A floor pattern keeps its pattern too, or it can never be applied to a room.
            await connection.ExecuteAsync("INSERT INTO items VALUES (16,1,0,103,'104',0,0)");
            Assert.Equal("104", (await new FurnitureInventoryLoader(database, definitions).Load(1)).Single(item => item.Id == 16).ExtraData.Serialize());
        }
        finally {
            await server.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    [RoomComponentDatabaseFact]
    public async Task ClearStoreRefundsChestCoinsAndKeepsChestsThatWouldOverflowTheWallet()
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "task_refactor_tests_inventory_" + Guid.NewGuid().ToString("N");
        var builder = new MySqlConnectionStringBuilder(root)
        {
            Database = "",
            Pooling = false,
            AllowZeroDateTime = true,
            ConvertZeroDateTime = true,
            AllowUserVariables = true
        };
        await using var server = new MySqlConnection(builder.ConnectionString);
        await server.OpenAsync();
        await server.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4");

        try {
            builder.Database = schema;
            await using var connection = new MySqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("""
                CREATE TABLE users (id INT PRIMARY KEY, credits INT NOT NULL);
                CREATE TABLE items (id INT UNSIGNED PRIMARY KEY, user_id INT NOT NULL, room_id INT UNSIGNED NOT NULL,
                    base_item INT UNSIGNED NOT NULL, extra_data TEXT NOT NULL, limited_number INT UNSIGNED NOT NULL,
                    limited_stack INT UNSIGNED NOT NULL);
                CREATE TABLE furniture (item_name VARCHAR(70) NOT NULL, interaction_type VARCHAR(64) NOT NULL);
                CREATE TABLE items_groups (id INT UNSIGNED PRIMARY KEY, group_id INT NOT NULL);
                CREATE TABLE items_builders_club (item_id INT UNSIGNED PRIMARY KEY, user_id INT NOT NULL);
                """);
            await connection.ExecuteAsync(RoomMusicDatabaseTests.Fixture.SchemaSql);
            await connection.ExecuteAsync(File.ReadAllText(Path.Combine(RoomMusicDatabaseTests.Fixture.Root(), "Database", "Migrations", "65_WiredChests.sql")));
            await connection.ExecuteAsync("""
                INSERT INTO users VALUES (1,100),(2,0),(3,2147483547);
                INSERT INTO items (id,user_id,room_id,base_item,extra_data,limited_number,limited_stack) VALUES
                    (10,1,0,100,'',0,0),(20,1,0,100,'',0,0),(21,1,0,100,'',0,0),(22,1,0,100,'',0,0),
                    (40,2,0,100,'',0,0),(50,3,0,100,'',0,0),(51,3,0,100,'',0,0),(52,3,0,100,'',0,0);
                INSERT INTO wired_chests (item_id,kind,coins,capacity_level,settings) VALUES
                    (20,1,0,0,'{}'),(21,1,250,0,'{}'),(22,0,0,0,'{}'),(40,1,5,0,'{}'),(50,1,100,0,'{}'),(51,1,101,0,'{}');
                INSERT INTO items (id,user_id,room_id,base_item,extra_data,limited_number,limited_stack,chest_item_id) VALUES
                    (30,0,0,100,'',0,0,22),(31,0,0,100,'',0,0,22),(41,0,0,100,'',0,0,40);
                """);
            var store = new InventoryClearStore(new TestDatabase(builder.ConnectionString), new Definitions(new ItemDefinition { Id = 100, Type = ItemType.Floor }));

            // The live wallet is authoritative, as for a chest withdrawal: 120 live plus 250 refunded.
            var cleared = store.DeleteAll(1, 120);

            Assert.Equal(new uint[] { 10, 20, 21, 22 }, cleared.Deleted.Order());
            Assert.Equal((370, 250L, 3, 0), (cleared.Credits, cleared.RefundedCredits, cleared.RefundedChests, cleared.KeptChests));
            Assert.Equal(370, await connection.ExecuteScalarAsync<int>("SELECT credits FROM users WHERE id=1"));
            Assert.Equal(new uint[] { 40, 41, 50, 51, 52 }, await connection.QueryAsync<uint>("SELECT id FROM items ORDER BY id"));
            Assert.Equal(new uint[] { 40, 50, 51 }, await connection.QueryAsync<uint>("SELECT item_id FROM wired_chests ORDER BY item_id"));
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM wired_chest_transactions WHERE user_id=1"));
            var entry = Assert.Single(await connection.QueryAsync<(uint Chest, uint? Item, bool Deposit, long Coins)>(
                "SELECT chest_id,item_id,is_deposit,coins FROM wired_chest_transaction_entries"));
            Assert.Equal((21u, (uint?)null, false, 250L), entry);

            var overflow = store.DeleteAll(3, int.MaxValue - 100);

            Assert.Equal(new uint[] { 50, 52 }, overflow.Deleted.Order());
            Assert.Equal((int.MaxValue, 100L, 1, 1), (overflow.Credits, overflow.RefundedCredits, overflow.RefundedChests, overflow.KeptChests));
            Assert.Equal(int.MaxValue, await connection.ExecuteScalarAsync<int>("SELECT credits FROM users WHERE id=3"));
            Assert.Equal(new uint[] { 51 }, await connection.QueryAsync<uint>("SELECT id FROM items WHERE user_id=3"));
            Assert.Equal(101, await connection.ExecuteScalarAsync<int>("SELECT coins FROM wired_chests WHERE item_id=51"));
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT credits FROM users WHERE id=2"));
        }
        finally {
            await server.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static TestContext Context(int userId = 1, uint itemId = 7)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 9;
        room.OwnerName = "owner";
        room.Type = "private";
        room.UsersWithRights = [];
        var users = new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel);
        typeof(Room).GetField("_roomUserManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, users);
        var definition = new ItemDefinition { Id = 100, Type = ItemType.Floor };
        var inventoryItem = new InventoryItem { Id = itemId, OwnerId = (uint)userId, Definition = definition };
        var habbo = new Habbo
        {
            Id = userId,
            Username = "owner",
            CurrentRoom = room,
            Inventory = new InventoryComponent { Furniture = new([inventoryItem], []) }
        };
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;
        var roomUser = new RoomUser(habbo.Id, room.Id, 1, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused) { UserId = habbo.Id };
        var entries = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
            .GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(users)!;
        entries[roomUser.VirtualId] = roomUser;
        var events = new List<string>();

        return new(room, roomUser, habbo, client, sent, events, new RecordingGate(events));
    }

    private sealed record TestContext(Room Room, RoomUser RoomUser, Habbo Habbo, GameClient Client,
        List<(uint Header, byte[] Payload)> Sent, List<string> Events, RecordingGate Gate);

    private sealed class RecordingStore(Action? before = null) : IInventoryClearStore
    {
        public int Calls { get; private set; }
        public Exception? Failure { get; init; }
        public InventoryClearResult? Result { get; init; }
        public int LiveCredits { get; private set; }

        public InventoryItem? AvailableDisc(uint discId, uint ownerId) => null;

        public InventoryClearResult DeleteAll(int userId, int liveCredits)
        {
            Calls++;
            LiveCredits = liveCredits;
            before?.Invoke();

            if (Failure != null) {
                throw Failure;
            }

            // The fixture inventory holds item 7 only.
            return Result ?? new([7], [], liveCredits, 0, 0, 0);
        }
    }

    private sealed class RecordingClearService : IInventoryClearService
    {
        public int Calls { get; private set; }
        public bool TryClear(GameClient session, Room room)
        {
            Calls++;

            return true;
        }
    }

    private sealed class RecordingGate(List<string> events) : IAccountSessionGate
    {
        public long Begin() => 0;
        public Task<IDisposable> EnterAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(Enter(userId));
        public Task<IDisposable> EnterManyAsync(IEnumerable<int> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult<IDisposable>(new Release(events));
        public IDisposable Enter(int userId)
        {
            events.Add("gate-enter");

            return new Release(events);
        }
        public void Revoke(int userId) { }
        public bool IsRevoked(int userId, long loginStarted) => false;
    }

    private sealed class Release(List<string> events) : IDisposable
    {
        public void Dispose() => events.Add("gate-exit");
    }

    private sealed class Definitions(params ItemDefinition[] definitions) : IItemDataManager
    {
        public Dictionary<uint, ItemDefinition> Items { get; } = definitions.ToDictionary(definition => definition.Id);
        public Dictionary<int, uint> Gifts { get; } = [];
        public void Init() { }
        public ItemDefinition? GetItemByName(string name) => null;
    }

    private sealed class TestDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }
}
