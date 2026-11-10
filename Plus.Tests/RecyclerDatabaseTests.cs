using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Recycler;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Items.DataFormat;
using Xunit;

namespace Plus.Tests;

public sealed class RecyclerDatabaseTests
{
    private static readonly DateTimeOffset Now = new(2040, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [RecyclerDatabaseFact]
    public void PurchasedChatStylePreferenceMigrationPreservesExistingSelectionsAndStoresLargeIds()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("ALTER TABLE users MODIFY COLUMN bubble_id TINYINT NOT NULL DEFAULT 0; UPDATE users SET bubble_id=127 WHERE id=7");
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("UPDATE users SET bubble_id=10001 WHERE id=7"));
        var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/134_ModernRecycler.sql"));
        fixture.Connection.Execute(migration);
        fixture.Connection.Execute(migration);
        Assert.Equal(127, fixture.Connection.ExecuteScalar<int>("SELECT bubble_id FROM users WHERE id=7"));
        fixture.Connection.Execute("UPDATE users SET bubble_id=10001 WHERE id=7");
        Assert.Equal(10001, fixture.Connection.ExecuteScalar<int>("SELECT bubble_id FROM users WHERE id=7"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT bubble_id FROM users WHERE id=8"));
    }

    [RecyclerDatabaseFact]
    public void RecyclerConsumesOnlyExactUniqueOwnedEligibleUnplacedRowsAndPersistsBoxContentAndCooldownTogether()
    {
        using var fixture = new Fixture();
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(20, 101)], Now));
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(22, 101)], Now)); // Foreign owner.
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(23, 101)], Now)); // Placed.
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(24, 103)], Now)); // Ineligible.
        fixture.Connection.Execute("UPDATE items SET limited_number=1 WHERE id=21");
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now));
        fixture.Connection.Execute("UPDATE items SET limited_number=0 WHERE id=21");
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Null(fixture.Store.NextAllowed(7));
        var box = Assert.IsType<RecycledBox>(fixture.Store.Recycle(7, 100, config, prize, [new(21, 101), new(20, 101)], Now));
        Assert.Equal("3-2-2040", box.ExtraData);
        Assert.Equal((7, 0u, 100u), fixture.Connection.QuerySingle<(int, uint, uint)>("SELECT user_id,room_id,base_item FROM items WHERE id=@id", new { id = box.Id }));
        Assert.Equal((102u, ""), fixture.Connection.QuerySingle<(uint, string)>("SELECT base_id,extra_data FROM user_presents WHERE item_id=@id", new { id = box.Id }));
        Assert.Equal(Now.AddSeconds(30), fixture.Store.NextAllowed(7));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(30,7,0,101,''),(31,7,0,101,'')");
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(30, 101), new(31, 101)], Now.AddSeconds(29.999)));
        Assert.NotNull(fixture.Store.Recycle(7, 100, config, prize, [new(30, 101), new(31, 101)], Now.AddSeconds(30)));
    }

    [RecyclerDatabaseFact]
    public void ReceiptsAndProgressSurviveOpeningAndCannotBeReplayedOrRewritten()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE recycler_settings SET cost_duckets=10; INSERT INTO user_currencies VALUES(7,0,20)");
        var configuration = fixture.Store.Load();
        var box = Assert.IsType<RecycledBox>(fixture.Store.Recycle(7, 100, configuration, configuration.Levels[0].Prizes[0],
            [new(20, 101), new(21, 101)], Now, 20));
        Assert.Equal((10, 10, 2), fixture.Connection.QuerySingle<(int, int, int)>(
            "SELECT cost_duckets,balance_after,JSON_LENGTH(inputs_json) FROM recycler_operations"));
        fixture.Connection.Execute("UPDATE items SET room_id=42 WHERE id=@id", new { id = box.Id });
        var content = fixture.Store.FindBox(box.Id, 7, 42, 100)!;
        var reward = Assert.IsType<uint>(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.Equal(reward, fixture.Connection.ExecuteScalar<uint>("SELECT reward_item_id FROM recycler_openings"));
        var progress = new RecyclerAchievementStore(fixture.Source);
        var expected = new RecyclerAchievementProgress(7, 8, 0, 0, 0);
        Assert.Equal(expected, progress.Read(7, new(5, 7, 0, 0, 0)));
        Assert.Equal(expected, progress.Read(7, new(999, 999, 999, 999, 999)));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("UPDATE recycler_operations SET cost_duckets=0"));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("DELETE FROM recycler_openings"));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("UPDATE recycler_revisions SET configuration_json='{}'"));
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(300,7,0,101,'')");
        Assert.NotNull(fixture.Store.Recycle(7, 100, configuration, configuration.Levels[0].Prizes[0],
            [new(reward, 102), new(300, 101)], Now.AddSeconds(31), 10));
        Assert.Equal(new RecyclerAchievementProgress(9, 8, 1, 0, 0), progress.Read(7, new(999, 999, 999, 999, 999)));
    }

    [RecyclerDatabaseFact]
    public void ChangingPrizesAfterRecyclingDoesNotChangeTheBoxOrItsRarityRewards()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO recycler_levels VALUES(6,5000); INSERT INTO recycler_prizes(id,level,item_id) VALUES(2,6,102)");
        var configuration = fixture.Store.Load();
        var prize = Assert.Single(configuration.Levels.Single(level => level.Id == 6).Prizes);
        var box = Assert.IsType<RecycledBox>(fixture.Store.Recycle(7, 100, configuration, prize, [new(20, 101), new(21, 101)], Now));
        fixture.Connection.Execute("UPDATE recycler_prizes SET item_id=103 WHERE id=2; UPDATE items SET room_id=42 WHERE id=@id", new { id = box.Id });
        var content = fixture.Store.FindBox(box.Id, 7, 42, 100)!;
        var reward = Assert.IsType<uint>(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.Equal(102u, fixture.Connection.ExecuteScalar<uint>("SELECT base_item FROM items WHERE id=@reward", new { reward }));
        Assert.Equal(new RecyclerAchievementProgress(2, 1, 0, 1, 1), new RecyclerAchievementStore(fixture.Source).Read(7, new(0, 0, 0, 0, 0)));
    }

    [RecyclerDatabaseFact]
    public void MissingAuditWriteRollsBackTheCostInputsAndBox()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            UPDATE recycler_settings SET cost_duckets=10;
            INSERT INTO user_currencies VALUES(7,0,20);
            CREATE TRIGGER reject_recycler_receipt BEFORE INSERT ON recycler_operations FOR EACH ROW
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced failure';
            """);
        var configuration = fixture.Store.Load();
        Assert.Throws<MySqlException>(() => fixture.Store.Recycle(7, 100, configuration, configuration.Levels[0].Prizes[0],
            [new(20, 101), new(21, 101)], Now, 20));
        Assert.Equal(20, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_progress"));
    }

    [RecyclerDatabaseFact]
    public void OpeningAuditFailureCannotConsumeTheBoxOrGrantItsContent()
    {
        using var fixture = new Fixture();
        var box = fixture.CreateBox();
        var content = fixture.Store.FindBox(box.Id, 7, 42, 100)!;
        fixture.Connection.Execute("""
            CREATE TRIGGER reject_recycler_opening BEFORE INSERT ON recycler_openings FOR EACH ROW
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced failure';
            """);
        Assert.Throws<MySqlException>(() => fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=@id AND base_item=100", new { id = box.Id }));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents WHERE item_id=@id", new { id = box.Id }));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=102"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_openings"));
    }

    [RecyclerDatabaseFact]
    public void RecyclerRefusesAContainerThatStillHasContents()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO user_presents(item_id,base_id,extra_data) VALUES(20,102,'inside')");
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);

        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents WHERE item_id=20"));
    }

    [RecyclerDatabaseFact]
    public void BoxContentAndCooldownInsertFailuresRollBackEveryConsumedRow()
    {
        foreach (var table in new[] { "items", "user_presents", "user_recycler" }) {
            using var fixture = new Fixture();
            fixture.Connection.Execute($"CREATE TRIGGER reject_recycler_output BEFORE INSERT ON {table} FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced recycler failure'");
            var config = fixture.Store.Load();
            var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
            Assert.Throws<MySqlException>(() => fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now));
            Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
            Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=100"));
            Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
            Assert.Null(fixture.Store.NextAllowed(7));
        }
    }

    [RecyclerDatabaseFact]
    public async Task ConcurrentRepeatedSubmissionsPayOnlyOnceAndDoNotConsumeTheSecondSetDuringCooldown()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(30,7,0,101,''),(31,7,0,101,'')");
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(30, 101), new(31, 101)], Now); }));
        Assert.Single(results, result => result is not null);
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21,30,31)"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=100"));
        Assert.Equal(Now.AddSeconds(30), fixture.Store.NextAllowed(7));
    }

    [RecyclerDatabaseFact]
    public void ConfigurationChangesOrOrphanPrizeRowsRefuseConsumptionAndMigrationRetainsOperatorData()
    {
        using var fixture = new Fixture();
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        fixture.Connection.Execute("UPDATE recycler_settings SET cooldown_seconds=31");
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now));
        fixture.Connection.Execute("INSERT INTO recycler_prizes(level,item_id) VALUES(5,102)");
        Assert.False(fixture.Store.Load().Enabled);
        fixture.Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/51_Recycler.sql")));
        Assert.Equal((true, 2, 31), fixture.Connection.QuerySingle<(bool, int, int)>("SELECT enabled,slots,cooldown_seconds FROM recycler_settings WHERE id=1"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_prizes"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
    }

    [RecyclerDatabaseFact]
    public void NativeEcotronContentsOpenOnceAsANewInventoryIdentityAndRejectForeignMalformedAndChangedContents()
    {
        using var fixture = new Fixture();
        var box = fixture.CreateBox();
        Assert.Null(fixture.Store.FindBox(box.Id, 8, 42, 100));
        Assert.Null(fixture.Store.FindBox(box.Id, 7, 43, 100));
        var content = Assert.IsType<GiftContent>(fixture.Store.FindBox(box.Id, 7, 42, 100));
        Assert.Null(fixture.Store.OpenBox(box.Id, 8, 42, 100, content));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 43, 100, content));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 101, content));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content with { ExtraData = "changed" }));
        fixture.Connection.Execute("INSERT INTO user_presents(item_id,base_id,extra_data) VALUES(@id,102,'duplicate')", new { id = box.Id });
        Assert.Null(fixture.Store.FindBox(box.Id, 7, 42, 100));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        fixture.Connection.Execute("DELETE FROM user_presents WHERE extra_data='duplicate'");
        fixture.Connection.Execute("UPDATE user_presents SET extra_data='blue' WHERE item_id=@id", new { id = box.Id });
        content = Assert.IsType<GiftContent>(fixture.Store.FindBox(box.Id, 7, 42, 100));
        var id = Assert.IsType<uint>(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.NotEqual(box.Id, id);
        Assert.Equal((7, 0u, 102u, "blue"), fixture.Connection.QuerySingle<(int, uint, uint, string)>("SELECT user_id,room_id,base_item,extra_data FROM items WHERE id=@id", new { id }));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=@id", new { id = box.Id }));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
    }

    [RecyclerDatabaseFact]
    public void FailedRewardCreationRestoresThePlacedBoxAndItsExactPayload()
    {
        using var fixture = new Fixture();
        var box = fixture.CreateBox();
        var content = Assert.IsType<GiftContent>(fixture.Store.FindBox(box.Id, 7, 42, 100));
        fixture.Connection.Execute("CREATE TRIGGER reject_open_reward BEFORE INSERT ON items FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced opening failure'");
        Assert.Throws<MySqlException>(() => fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=@id AND user_id=7 AND room_id=42 AND base_item=100", new { id = box.Id }));
        Assert.Equal(content, fixture.Store.FindBox(box.Id, 7, 42, 100));
        fixture.Connection.Execute("DROP TRIGGER reject_open_reward");
        Assert.NotNull(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
    }

    [RecyclerDatabaseFact]
    public async Task ReloadedNativeInventoryPreservesEcotronDateAndCategoryWhileOtherDefaultDataStaysUnchanged()
    {
        using var fixture = new Fixture();
        var box = fixture.CreateBox();
        fixture.Connection.Execute("UPDATE items SET room_id=0 WHERE id=@id; UPDATE items SET extra_data='unused default state' WHERE id=24", new { id = box.Id });
        var definitions = new ItemDataManager(TestLogging.For<ItemDataManager>(), fixture.Source);
        definitions.Init();
        var inventory = await new Plus.HabboHotel.Users.Inventory.Furniture.FurnitureInventoryLoader(fixture.Source, definitions).Load(7);
        var loaded = Assert.Single(inventory.Where(item => item.Id == box.Id));
        Assert.Equal("3-2-2040", loaded.ExtraData.Serialize());
        Assert.Equal(Plus.HabboHotel.Users.Inventory.Furniture.FurniCategory.EcotronBox,
            Plus.HabboHotel.Users.Inventory.Furniture.InventoryItemSnapshot.Capture(loaded).Category);
        Assert.Equal("", Assert.Single(inventory.Where(item => item.Id == 24)).ExtraData.Serialize());
    }

    [RecyclerDatabaseFact]
    public async Task ConcurrentOpeningCreatesOnlyOneRewardAndDeletesOneExactBoxPayload()
    {
        using var fixture = new Fixture();
        var box = fixture.CreateBox();
        var content = Assert.IsType<GiftContent>(fixture.Store.FindBox(box.Id, 7, 42, 100));
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Store.OpenBox(box.Id, 7, 42, 100, content); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.OpenBox(box.Id, 7, 42, 100, content); }));
        Assert.Single(results, result => result is not null);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=102 AND user_id=7 AND room_id=0"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
    }

    [RecyclerDatabaseFact]
    public async Task ABlockedOwnersInputDoesNotBlockAnotherOwnerUsingTheSameConfigurationAndFurniture()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(25,8,0,101,'')");
        var config = fixture.Store.Load();
        var prize = config.Levels[0].Prizes[0];
        using var holder = new MySqlConnection(fixture.ConnectionString);
        holder.Open();
        using var held = holder.BeginTransaction();
        holder.ExecuteScalar<uint>("SELECT id FROM items WHERE id=20 FOR UPDATE", transaction: held);
        Task<RecycledBox?>? first = null;
        Task<RecycledBox?>? second = null;
        var firstThread = 0;
        fixture.ConnectionOpened = thread => Interlocked.CompareExchange(ref firstThread, thread, 0);

        try {
            first = Task.Factory.StartNew(() => fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            var waiting = false;
            var deadline = DateTime.UtcNow.AddSeconds(5);

            while (!waiting && DateTime.UtcNow < deadline) {
                waiting = fixture.Connection.ExecuteScalar<int>("""
                    SELECT COUNT(*) FROM information_schema.INNODB_LOCK_WAITS w
                    INNER JOIN information_schema.INNODB_TRX t ON t.trx_id=w.requesting_trx_id
                    INNER JOIN information_schema.INNODB_TRX blocker ON blocker.trx_id=w.blocking_trx_id
                    WHERE t.trx_mysql_thread_id=@thread AND blocker.trx_mysql_thread_id=@holder
                    """, new { thread = Volatile.Read(ref firstThread), holder = holder.ServerThread }) > 0;

                if (!waiting) {
                    await Task.Delay(250);
                }
            }

            if (!waiting) {
                var states = fixture.Connection.Query<string>("SELECT CONCAT(trx_state,':',COALESCE(trx_query,'')) FROM information_schema.INNODB_TRX WHERE trx_mysql_thread_id=@thread", new { thread = Volatile.Read(ref firstThread) });
                var result = first.IsCompleted ? (await first is null ? "refused" : "committed") : first.Status.ToString();
                Console.WriteLine($"Owned connection {firstThread}, task {result}: {string.Join(" | ", states)}");
            }

            Assert.True(waiting, $"First owner never reached the deliberately locked input (task: {first.Status}).");
            second = Task.Factory.StartNew(() => fixture.Store.Recycle(8, 100, config, prize, [new(22, 101), new(25, 101)], Now),
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.Same(second, await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(5))));
            Assert.NotNull(await second);
            Assert.False(first.IsCompleted);
        }
        finally {
            held.Rollback();

            if (first is not null) {
                await first;
            }

            if (second is not null) {
                await second;
            }
        }

        Assert.NotNull(await first!);
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_recycler"));
    }

    [RecyclerDatabaseFact]
    public void DucketChargeAndBoxConsumptionCommitTogetherAndCannotRepeat()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE recycler_settings SET cost_duckets=10; INSERT INTO user_currencies VALUES(7,0,100)");
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now, 9));
        Assert.Equal(100, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        var box = Assert.IsType<RecycledBox>(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now, 100));
        Assert.Equal(90, box.Duckets);
        Assert.Equal(90, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        Assert.Null(fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now.AddHours(1), 90));
        Assert.Equal(90, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
    }

    [RecyclerDatabaseFact]
    public async Task ZeroCooldownRecyclesTwiceButChargesEachBox()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE recycler_settings SET cooldown_seconds=0, cost_duckets=10; " +
            "INSERT INTO user_currencies VALUES(7,0,100); " +
            "INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(30,7,0,101,''),(31,7,0,101,'')");
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        using var start = new Barrier(2);
        var results = await Task.WhenAll(
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now, 100); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(30, 101), new(31, 101)], Now, 100); }));

        Assert.All(results, result => Assert.NotNull(result));
        Assert.Equal(80, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=100"));
    }

    [RecyclerDatabaseFact]
    public async Task ConcurrentRecyclingWithStaleBalancesCannotOverspend()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE recycler_settings SET cooldown_seconds=0, cost_duckets=10; " +
            "INSERT INTO user_currencies VALUES(7,0,10); " +
            "INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(30,7,0,101,''),(31,7,0,101,'')");
        var config = fixture.Store.Load();
        var prize = Assert.Single(Assert.Single(config.Levels).Prizes);
        using var start = new Barrier(2);
        var results = await Task.WhenAll(
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(20, 101), new(21, 101)], Now, 100); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.Recycle(7, 100, config, prize, [new(30, 101), new(31, 101)], Now, 100); }));

        Assert.Single(results, result => result is not null);
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE base_item=100"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21,30,31)"));
    }

    [RecyclerDatabaseFact]
    public void FailedChargeRollsBackInputsBoxContentsAndCooldown()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("UPDATE recycler_settings SET cost_duckets=10; INSERT INTO user_currencies VALUES(7,0,100); CREATE TRIGGER refuse_charge BEFORE INSERT ON user_currencies FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced failure'");
        var config = fixture.Store.Load();
        Assert.Throws<MySqlException>(() => fixture.Store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now, 100));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_presents"));
        Assert.Equal(100, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
        Assert.Null(fixture.Store.NextAllowed(7));
    }

    [RecyclerDatabaseFact]
    public async Task ChatStyleBoxOpensOnceUnderConcurrentRequestsAndPersistsOwnership()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO room_chat_styles(id,requires_ownership) VALUES(10001,TRUE); UPDATE recycler_prizes SET item_id=NULL,chat_style_id=10001");
        var config = fixture.Store.Load();
        var box = Assert.IsType<RecycledBox>(fixture.Store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now));
        fixture.Connection.Execute("UPDATE items SET room_id=42 WHERE id=@id", new { id = box.Id });
        var content = Assert.IsType<GiftContent>(fixture.Store.FindBox(box.Id, 7, 42, 100));
        Assert.Equal(10001, content.ChatStyleId);
        Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
        Assert.False(fixture.Store.OpenChatStyleBox(box.Id, 8, 42, 100, content));
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Store.OpenChatStyleBox(box.Id, 7, 42, 100, content); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Store.OpenChatStyleBox(box.Id, 7, 42, 100, content); }));
        Assert.Single(results, result => result);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_purchased_chat_styles WHERE user_id=7 AND style_id=10001"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=@id", new { id = box.Id }));
        Assert.False(fixture.Store.OpenChatStyleBox(box.Id, 7, 42, 100, content));
    }

    [RecyclerDatabaseFact]
    public void NewCostConfigurationCannotBeBypassedWithAnOldSnapshot()
    {
        using var fixture = new Fixture();
        var old = fixture.Store.Load();
        fixture.Connection.Execute("UPDATE recycler_settings SET cost_duckets=10");
        Assert.Null(fixture.Store.Recycle(7, 100, old, old.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));
    }

    [RecyclerDatabaseFact]
    public void LostCommitResponseRecoversTheSameBoxAndDoesNotChargeTwice()
    {
        foreach (var commit in new[] { false, true }) {
            using var fixture = new Fixture();
            fixture.Connection.Execute("UPDATE recycler_settings SET cost_duckets=10; INSERT INTO user_currencies VALUES(7,0,20)");
            var config = fixture.Store.Load();
            var store = new RecyclerStore(new RecyclerCommitFaultDatabase(fixture.Source, commit));
            var box = store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now, 20);
            Assert.Equal(commit, box is not null);
            Assert.Equal(commit ? 10 : 20, fixture.Connection.ExecuteScalar<int>("SELECT amount FROM user_currencies WHERE user_id=7 AND type=0"));
            Assert.Equal(commit ? 1 : 0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_operations"));
            Assert.Equal(commit ? 0 : 2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id IN (20,21)"));

            if (commit) {
                Assert.Equal(box!.Id, fixture.Connection.ExecuteScalar<uint>("SELECT box_item_id FROM recycler_operations"));
                Assert.Null(fixture.Store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now, 20));
            }
        }
    }

    [RecyclerDatabaseFact]
    public void LostOpeningCommitResponseRecoversFurnitureAndChatStyleWithoutAnotherReward()
    {
        foreach (var style in new[] { 0, 10001 }) {
            foreach (var commit in new[] { false, true }) {
                using var fixture = new Fixture();

                if (style > 0) {
                    fixture.Connection.Execute("INSERT INTO room_chat_styles(id,requires_ownership) VALUES(10001,TRUE); UPDATE recycler_prizes SET item_id=NULL,chat_style_id=10001");
                }

                var box = fixture.CreateBox();
                var content = fixture.Store.FindBox(box.Id, 7, 42, 100)!;
                var store = new RecyclerStore(new RecyclerCommitFaultDatabase(fixture.Source, commit));
                var opened = style > 0 ? store.OpenChatStyleBox(box.Id, 7, 42, 100, content)
                    : store.OpenBox(box.Id, 7, 42, 100, content) is not null;
                Assert.Equal(commit, opened);
                Assert.Equal(commit ? 1 : 0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_openings"));
                Assert.Equal(commit ? 0 : 1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=@id", new { id = box.Id }));

                if (commit) {
                    Assert.Null(fixture.Store.OpenBox(box.Id, 7, 42, 100, content));
                    Assert.False(fixture.Store.OpenChatStyleBox(box.Id, 7, 42, 100, content));
                }
            }
        }
    }

    [RecyclerDatabaseFact]
    public void UnavailableCommitRecoveryReportsAnUnknownOutcomeAndKeepsTheReceipt()
    {
        using var fixture = new Fixture();
        var config = fixture.Store.Load();
        var store = new RecyclerStore(new RecyclerCommitFaultDatabase(fixture.Source, commit: true, recoveryAvailable: false));
        Assert.Throws<RecyclerOutcomeUnknownException>(() => store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_operations"));
        Assert.Equal(2, new RecyclerAchievementStore(fixture.Source).Read(7, new(0, 0, 0, 0, 0))!.Items);
    }

    [RecyclerDatabaseFact]
    public void TwoBoxesWithTheSameChatStyleKeepOnePermanentRightAndTwoOpeningReceipts()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO room_chat_styles(id,requires_ownership) VALUES(10001,TRUE); UPDATE recycler_prizes SET item_id=NULL,chat_style_id=10001");
        var first = fixture.CreateBox();
        Assert.True(fixture.Store.OpenChatStyleBox(first.Id, 7, 42, 100, fixture.Store.FindBox(first.Id, 7, 42, 100)!));
        fixture.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(300,7,0,101,''),(301,7,0,101,'')");
        var config = fixture.Store.Load();
        var second = fixture.Store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(300, 101), new(301, 101)], Now.AddMinutes(1))!;
        fixture.Connection.Execute("UPDATE items SET room_id=42 WHERE id=@id", new { id = second.Id });
        Assert.True(fixture.Store.OpenChatStyleBox(second.Id, 7, 42, 100, fixture.Store.FindBox(second.Id, 7, 42, 100)!));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_purchased_chat_styles"));
        Assert.Equal(2, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_openings"));
        Assert.Equal(2, new RecyclerAchievementStore(fixture.Source).Read(7, new(0, 0, 0, 0, 0))!.Boxes);
    }

    [RecyclerDatabaseFact]
    public async Task AcquiredStylesReloadOnlyForTheirOwnerAndMigrationsPreservePendingBoxesAndReceipts()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO room_chat_styles(id,requires_ownership) VALUES(10001,TRUE); UPDATE recycler_prizes SET item_id=NULL,chat_style_id=10001");
        var box = fixture.CreateBox();

        foreach (var migration in new[] { "134_ModernRecycler.sql", "135_RecyclerReceipts.sql" }) {
            fixture.Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/" + migration)));
        }

        Assert.True(fixture.Store.OpenChatStyleBox(box.Id, 7, 42, 100, fixture.Store.FindBox(box.Id, 7, 42, 100)!));
        var owner = new Habbo { Id = 7 };
        var other = new Habbo { Id = 8 };
        var loader = new Plus.HabboHotel.Rooms.Chat.Styles.PurchasedChatStylesLoadingTask(fixture.Source);
        await loader.Load(owner);
        await loader.Load(other);
        Assert.Equal(10001, Assert.Single(owner.PurchasedChatStyles).Key);
        Assert.Empty(other.PurchasedChatStyles);
        fixture.Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/135_RecyclerReceipts.sql")));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_operations"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM recycler_openings"));
    }

    [RecyclerDatabaseFact]
    public void LegacyBoxesOpenWithoutInventedRarityAndTransferredBoxesRewardTheirCurrentOwner()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(300,7,42,100,'3-2-2040');
            INSERT INTO user_presents(item_id,base_id,extra_data) VALUES(300,102,'legacy');
            """);
        var legacy = fixture.Store.FindBox(300, 7, 42, 100)!;
        Assert.NotNull(fixture.Store.OpenBox(300, 7, 42, 100, legacy));
        Assert.Equal(new RecyclerAchievementProgress(0, 1, 0, 0, 0), new RecyclerAchievementStore(fixture.Source).Read(7, new(0, 0, 0, 0, 0)));
        var box = fixture.CreateBox();
        fixture.Connection.Execute("UPDATE items SET user_id=8 WHERE id=@id", new { id = box.Id });
        Assert.Null(fixture.Store.FindBox(box.Id, 7, 42, 100));
        var content = fixture.Store.FindBox(box.Id, 8, 42, 100)!;
        var reward = fixture.Store.OpenBox(box.Id, 8, 42, 100, content);
        Assert.NotNull(reward);
        Assert.Equal(8, fixture.Connection.ExecuteScalar<int>("SELECT user_id FROM items WHERE id=@reward", new { reward }));
        Assert.Equal(new RecyclerAchievementProgress(0, 1, 0, 0, 0), new RecyclerAchievementStore(fixture.Source).Read(8, new(0, 0, 0, 0, 0)));
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MySqlConnection _admin;
        private readonly string _schema = "task_recycler_" + Guid.NewGuid().ToString("N");
        public MySqlConnection Connection { get; }
        // An open connection drops its password from ConnectionString, so keep the original.
        public string ConnectionString { get; }
        public RecyclerStore Store { get; }
        public IDatabase Source { get; }
        public Action<int>? ConnectionOpened { get; set; }
        public Fixture()
        {
            SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
            var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RECYCLER_DATABASE")!)
            { Database = "information_schema", Pooling = false, AllowZeroDateTime = true, ConvertZeroDateTime = true };
            _admin = new MySqlConnection(options.ConnectionString);
            _admin.Open();
            _admin.Execute($"CREATE DATABASE `{_schema}`");
            options.Database = _schema;
            ConnectionString = options.ConnectionString;
            Connection = new MySqlConnection(options.ConnectionString);
            Source = new Database(options.ConnectionString, thread => ConnectionOpened?.Invoke(thread));
            Store = new RecyclerStore(Source, new Microsoft.Extensions.Time.Testing.FakeTimeProvider(Now));

            try {
                Connection.Open();
                var dump = File.ReadAllText(HabbiconPacketTests.Repo("Resources/SQLs/Original Database.sql"));

                foreach (var name in new[] { "items", "furniture", "user_presents", "items_groups" }) {
                    var start = dump.IndexOf($"CREATE TABLE `{name}` (", StringComparison.Ordinal);
                    Connection.Execute(dump[start..(dump.IndexOf(';', start) + 1)]);
                }

                Connection.Execute("CREATE TABLE users(id INT PRIMARY KEY,bubble_id TINYINT NOT NULL DEFAULT 0); INSERT INTO users(id) VALUES(7),(8)");
                // The inventory loader also reads linked discs and Builders Club placements.
                Connection.Execute(RoomMusicDatabaseTests.Fixture.SchemaSql);
                Connection.Execute("CREATE TABLE items_builders_club(item_id INT UNSIGNED PRIMARY KEY,user_id INT NOT NULL)");
                Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/51_Recycler.sql")));
                Connection.Execute("CREATE TABLE room_chat_styles(id INT PRIMARY KEY,enabled BOOL DEFAULT TRUE,requires_hc BOOL DEFAULT FALSE,required_permission VARCHAR(191) DEFAULT ''); CREATE TABLE user_currencies(user_id INT,type INT,amount INT,PRIMARY KEY(user_id,type))");
                Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/134_ModernRecycler.sql")));
                Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/135_RecyclerReceipts.sql")));
                Assert.False(Store.Load().Enabled);
                Assert.Empty(Store.Load().Levels);
                Connection.Execute("""
                    UPDATE recycler_settings SET enabled=TRUE,slots=2,cooldown_seconds=30;
                    INSERT INTO recycler_levels(level,chance) VALUES(1,1);
                    INSERT INTO recycler_prizes(id,level,item_id) VALUES(1,1,102);
                    INSERT INTO furniture(id,item_name,sprite_id,allow_recycle) VALUES(100,'ecotron_box',3095,FALSE),(101,'input',41,TRUE),(102,'reward',42,TRUE),(103,'not_recyclable',43,FALSE);
                    INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(20,7,0,101,''),(21,7,0,101,''),(22,8,0,101,''),(23,7,42,101,''),(24,7,0,103,'');
                    """);
            }
            catch {
                Dispose();
                throw;
            }
        }
        public RecycledBox CreateBox()
        {
            var config = Store.Load();
            var box = Assert.IsType<RecycledBox>(Store.Recycle(7, 100, config, config.Levels[0].Prizes[0], [new(20, 101), new(21, 101)], Now));
            Connection.Execute("UPDATE items SET room_id=42 WHERE id=@id", new { id = box.Id });

            return box;
        }
        public void Dispose()
        {
            Connection.Dispose();
            _admin.Execute($"DROP DATABASE IF EXISTS `{_schema}`");
            _admin.Dispose();
        }
        private sealed class Database(string connectionString, Action<int> opened) : IDatabase
        {
            public bool IsConnected() => true;
            public IDbConnection Connection()
            {
                var connection = new MySqlConnection(connectionString);
                connection.StateChange += (_, state) =>
                {
                    if (state.CurrentState == ConnectionState.Open) {
                        opened(connection.ServerThread);
                    }
                };

                return connection;
            }
        }
    }
}

public sealed class RecyclerDatabaseFactAttribute : FactAttribute
{
    public RecyclerDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RECYCLER_DATABASE"))) {
            Skip = "Set RECYCLER_DATABASE for isolated native-schema recycler regressions.";
        }
    }
}
