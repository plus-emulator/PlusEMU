using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Authentication;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Plus.Tests;

public sealed class MarketplaceCancellationDatabaseTests
{
    [RecyclerDatabaseFact]
    public async Task TwoBuyersCannotReceiveTheSameOffer()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("INSERT INTO users VALUES(9,1000)");
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Buy(); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Buy(buyerId: 9); }));
        Assert.Single(results, result => result.Offer != null);
        Assert.Single(results, result => result.Refusal == MarketplacePurchaseRefusal.Sold);
        Assert.Equal(1900, fixture.Connection.QuerySingle<int>("SELECT SUM(credits) FROM users WHERE id IN(8,9)"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id=77 AND user_id IN(8,9)"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM marketplace_sales"));
    }

    [RecyclerDatabaseFact]
    public async Task ConcurrentOffersCannotSpendTheSameStoredCreditsTwice()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("""
            UPDATE catalog_marketplace_offers SET total_price=700,asking_price=699;
            INSERT INTO catalog_marketplace_offers(offer_id,state,user_id,furni_id,item_id,extra_data,limited_number,limited_stack,total_price,asking_price,listed_at)
            VALUES(2,'1',7,78,10,'other',4,10,700,699,'2040-01-01 00:00:00');
            """);
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Buy(); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Buy(offerId: 2); }));
        Assert.Single(results, result => result.Offer != null);
        Assert.Single(results, result => result.Refusal == MarketplacePurchaseRefusal.InsufficientCredits);
        Assert.Equal(300, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=8"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items"));
    }

    [RecyclerDatabaseFact]
    public void ListingRecoversBothCommittedAndRolledBackResponses()
    {
        foreach (var committed in new[] { true, false }) {
            using var fixture = new Fixture();
            fixture.Connection.Execute("CREATE TABLE room_music_playlist(player_id INT,disc_id INT); INSERT INTO items(id,base_item,user_id,room_id,extra_data) VALUES(88,10,7,0,'kept')");
            var store = new MarketplaceOfferStore(new MarketplaceCommitFaultDatabase(fixture.Source, committed));
            var listing = new MarketplaceListing(88, 10, 7, 99, 100, "chair", 42, "1", DateTimeOffset.UtcNow, "kept", 0, 0);

            if (committed) {
                Assert.True(store.ListFurni(listing));
                Assert.Equal(0, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id=88"));
            }
            else {
                Assert.Throws<MarketplaceCommitFailedException>(() => store.ListFurni(listing));
                Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items WHERE id=88"));
            }

            Assert.Equal(committed ? 1 : 0, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM catalog_marketplace_offers WHERE furni_id=88"));
        }
    }

    [RecyclerDatabaseFact]
    public void RedemptionRecoversBothCommittedAndRolledBackResponses()
    {
        foreach (var committed in new[] { true, false }) {
            using var fixture = new Fixture();
            fixture.Connection.Execute("UPDATE catalog_marketplace_offers SET state='2'");
            var store = new MarketplaceOfferStore(new MarketplaceCommitFaultDatabase(fixture.Source, committed));

            if (committed) {
                Assert.Equal(1099, store.ClaimSold(7, owed => 1000 + owed));
                Assert.Null(new MarketplaceOfferStore(fixture.Source).ClaimSold(7, owed => 1000 + owed));
            }
            else {
                Assert.Throws<MarketplaceCommitFailedException>(() => store.ClaimSold(7, owed => 1000 + owed));
            }

            Assert.Equal(committed ? 1099 : 0, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=7"));
            Assert.Equal(committed ? 0 : 1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
        }
    }

    [RecyclerDatabaseFact]
    public void MigrationCanRepeatWithoutChangingSalesOrReceipts()
    {
        using var fixture = new Fixture();
        Assert.NotNull(fixture.Buy().Offer);
        fixture.Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/136_MarketplaceSales.sql")));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM marketplace_sales"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM marketplace_operations"));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("DELETE FROM marketplace_operations"));
        Assert.Throws<MySqlException>(() => fixture.Connection.Execute("UPDATE marketplace_sales SET total_price=0"));
    }

    [RecyclerDatabaseFact]
    public void PurchasePreservesPendingLiveCurrencyWithoutSpendingItTwice()
    {
        using var fixture = new Fixture();
        var result = fixture.Buy(liveCredits: 1010);
        Assert.Equal(910, result.Offer!.BuyerCredits);
        Assert.Equal(900, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=8"));
        Assert.Equal(MarketplacePurchaseRefusal.Sold, fixture.Buy().Refusal);
    }

    [RecyclerDatabaseFact]
    public void PurchaseRecoversACommittedLostResponseWithoutAnotherCharge()
    {
        using var fixture = new Fixture();
        var result = fixture.Buy(new MarketplaceCommitFaultDatabase(fixture.Source, true));
        Assert.NotNull(result.Offer);
        Assert.Equal(900, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=8"));
        Assert.Equal(MarketplacePurchaseRefusal.Sold, fixture.Buy().Refusal);
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM marketplace_operations"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items"));
    }

    [RecyclerDatabaseFact]
    public void PurchaseRecoversARolledBackLostResponseWithoutDelivery()
    {
        using var fixture = new Fixture();
        Assert.Throws<MarketplaceCommitFailedException>(() => fixture.Buy(new MarketplaceCommitFaultDatabase(fixture.Source, false)));
        Assert.Equal(1000, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=8"));
        Assert.Equal("1", fixture.Connection.QuerySingle<string>("SELECT state FROM catalog_marketplace_offers"));
        Assert.Equal(0, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items"));
        Assert.NotNull(fixture.Buy().Offer);
    }

    [RecyclerDatabaseFact]
    public void UnknownCommitClosesTheWalletInsteadOfSavingAStaleBalance()
    {
        using var fixture = new Fixture();
        var habbo = new Plus.HabboHotel.Users.Habbo { Id = 8, Credits = 1000 };
        Assert.Throws<MarketplaceOutcomeUnknownException>(() => MarketplaceCommit.Guard(habbo,
            () => fixture.Buy(new MarketplaceCommitFaultDatabase(fixture.Source, true, false))));
        Assert.True(habbo.CurrencyPersistenceUncertain);
        Assert.True(habbo.WalletClosed);
        Assert.Equal(900, fixture.Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=8"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items"));
    }

    [RecyclerDatabaseFact]
    public void CancellationRecoversACommittedLostResponse()
    {
        using var fixture = new Fixture();
        Assert.NotNull(fixture.Cancel(source: new MarketplaceCommitFaultDatabase(fixture.Source, true)));
        Assert.Null(fixture.Cancel());
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM items"));
        Assert.Equal(1, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM marketplace_operations"));
    }

    [RecyclerDatabaseFact]
    public void CancellationPreservesIdentityAndContentsAndCannotRepeat()
    {
        using var fixture = new Fixture();
        var item = Assert.IsType<InventoryItem>(fixture.Cancel());
        Assert.Equal(77u, item.Id);
        Assert.Equal(7u, item.OwnerId);
        Assert.Equal("hello", fixture.Connection.ExecuteScalar<string>("SELECT extra_data FROM items WHERE id=77"));
        Assert.Equal((3, 10), fixture.Connection.QuerySingle<(int, int)>("SELECT limited_number,limited_stack FROM items WHERE id=77"));
        Assert.Null(fixture.Cancel());
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
    }

    [RecyclerDatabaseFact]
    public void SoldAndForeignOffersCannotBeReturned()
    {
        using var fixture = new Fixture();
        Assert.Null(fixture.Cancel(owner: 8));
        fixture.Connection.Execute("UPDATE catalog_marketplace_offers SET state='2'");
        Assert.Null(fixture.Cancel());
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
    }

    [RecyclerDatabaseFact]
    public async Task ConcurrentCancellationsReturnOnlyOneItem()
    {
        using var fixture = new Fixture();
        using var start = new Barrier(2);
        var results = await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); return fixture.Cancel(); }),
            Task.Run(() => { start.SignalAndWait(); return fixture.Cancel(); }));
        Assert.Single(results, item => item is not null);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
    }

    [RecyclerDatabaseFact]
    public async Task PurchaseRacingCancellationHasExactlyOneRecipient()
    {
        using var fixture = new Fixture();
        using var start = new Barrier(2);
        InventoryItem? returned = null;
        MarketplacePurchaseResult? bought = null;
        await Task.WhenAll(Task.Run(() => { start.SignalAndWait(); returned = fixture.Cancel(); }),
            Task.Run(() => { start.SignalAndWait(); bought = fixture.Buy(); }));
        Assert.NotNull(bought);
        Assert.NotEqual(returned is not null, bought!.Offer is not null);
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=77"));
        var owner = fixture.Connection.ExecuteScalar<int>("SELECT user_id FROM items WHERE id=77");
        Assert.Equal(returned is null ? 8 : 7, owner);
        Assert.Equal(returned is null ? 900 : 1000, fixture.Connection.ExecuteScalar<int>("SELECT credits FROM users WHERE id=8"));
    }

    [RecyclerDatabaseFact]
    public void FailedRemovalOrPreparationLeavesOfferAndNoReturnedFurniture()
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Store.Cancel(7, 1, _ => fixture.Definition,
            _ => throw new InvalidOperationException("Invalid furniture")));
        fixture.Connection.Execute("CREATE TRIGGER refuse_cancel BEFORE DELETE ON catalog_marketplace_offers FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced failure'");
        Assert.Throws<MySqlException>(() => fixture.Cancel());
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
    }

    [RecyclerDatabaseFact]
    public async Task ExpiredOffersReturnToAnOfflineOwnerOnlyOnce()
    {
        using var fixture = new Fixture();
        var definitions = CatalogSnapshotTestSupport.Proxy<IItemDataManager>((method, _) => method == "get_Items"
            ? new Dictionary<uint, ItemDefinition> { [10] = fixture.Definition } : throw new InvalidOperationException(method));
        var clients = CatalogSnapshotTestSupport.Proxy<IGameClientManager>((method, _) => method == "GetClientByUserId" ? null : throw new InvalidOperationException(method));
        var policy = new MarketplaceFeePolicy(new TestRoomSettings(new() { ["catalog.marketplace.expiration_hours"] = "168" }));
        var clock = new Microsoft.Extensions.Time.Testing.FakeTimeProvider(new DateTimeOffset(2040, 1, 8, 0, 0, 1, TimeSpan.Zero));
        var logger = new ExpiryLogger();
        using var service = new MarketplaceExpiryService(fixture.Source, definitions, clients, new AccountSessionGate(), policy, clock, logger);
        var returned = await service.RunBatch();
        Assert.Empty(logger.Errors);
        Assert.Equal(1, returned);
        Assert.Equal(0, await service.RunBatch());
        Assert.Equal((7, 10u, "hello", 3, 10), fixture.Connection.QuerySingle<(int, uint, string, int, int)>(
            "SELECT user_id,base_item,extra_data,limited_number,limited_stack FROM items WHERE id=77"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
    }

    [RecyclerDatabaseFact]
    public void ExpirationIsRevalidatedUnderTheOfferLock()
    {
        using var fixture = new Fixture();
        Assert.Null(fixture.Store.Cancel(7, 1, _ => fixture.Definition, _ => throw new InvalidOperationException("Must not prepare an active offer"),
            new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Equal(1, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM catalog_marketplace_offers"));
        Assert.Equal(0, fixture.Connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items"));
    }

    private sealed class Database(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public System.Data.IDbConnection Connection() => new MySqlConnection(connectionString);
    }

    private sealed class ExpiryLogger : ILogger<MarketplaceExpiryService>
    {
        public List<Exception> Errors { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        {
            if (level >= LogLevel.Error) {
                Errors.Add(error ?? new InvalidOperationException(format(state, error)));
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly MySqlConnection _admin;
        private readonly string _schema = "task_marketplace_cancel_" + Guid.NewGuid().ToString("N");
        public MySqlConnection Connection { get; }
        public MarketplaceCancellationStore Store { get; }
        private readonly IDatabase _database;
        public IDatabase Source => _database;
        public ItemDefinition Definition { get; } = new() { Id = 10, ItemName = "chair", SpriteId = 42, Type = ItemType.Floor, InteractionType = InteractionType.None };

        public Fixture()
        {
            SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
            var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("RECYCLER_DATABASE")!) { Database = "information_schema", Pooling = false };
            _admin = new MySqlConnection(options.ConnectionString);
            _admin.Open();
            _admin.Execute($"CREATE DATABASE `{_schema}`");
            options.Database = _schema;
            _database = new Database(options.ConnectionString);
            Store = new MarketplaceCancellationStore(_database);
            Connection = new MySqlConnection(options.ConnectionString);
            Connection.Open();
            Connection.Execute("""
                CREATE TABLE catalog_marketplace_offers(offer_id INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,state VARCHAR(1) DEFAULT '1',user_id INT,furni_id INT UNSIGNED,item_id INT UNSIGNED,extra_data TEXT,limited_number INT,limited_stack INT,total_price INT,asking_price INT DEFAULT 99,listed_at DATETIME(6),public_name VARCHAR(100) DEFAULT '',sprite_id INT UNSIGNED DEFAULT 42,item_type ENUM('1','2') DEFAULT '1') ENGINE=InnoDB;
                CREATE TABLE items(id INT UNSIGNED PRIMARY KEY,base_item INT UNSIGNED,user_id INT,room_id INT,x INT,y INT,z DOUBLE,wall_pos TEXT,rot INT,extra_data TEXT,limited_number INT,limited_stack INT) ENGINE=InnoDB;
                CREATE TABLE users(id INT PRIMARY KEY,credits INT); INSERT INTO users VALUES(7,0),(8,1000);
                CREATE TABLE catalog_marketplace_data(id INT AUTO_INCREMENT PRIMARY KEY,sprite INT,sold INT,avgprice INT);
                INSERT INTO catalog_marketplace_offers(offer_id,state,user_id,furni_id,item_id,extra_data,limited_number,limited_stack,total_price,asking_price,listed_at)
                VALUES(1,'1',7,77,10,'hello',3,10,100,99,'2040-01-01 00:00:00');
                """);
            Connection.Execute(File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/136_MarketplaceSales.sql")));
        }

        public MarketplacePurchaseResult Buy(IDatabase? source = null, int liveCredits = 1000, int buyerId = 8, int offerId = 1) => new MarketplacePurchaseStore(source ?? _database).Claim(new MarketplacePurchaseRequest(
            offerId, buyerId, liveCredits, new DateTimeOffset(2039, 12, 31, 0, 0, 0, TimeSpan.Zero), _ => Definition,
            delivery => new InventoryItem
            {
                Id = delivery.FurniId,
                OwnerId = (uint)buyerId,
                Definition = Definition,
                ExtraData = FurniExtraData.Load(Definition, delivery.ExtraData, keepLegacy: true),
                UniqueNumber = delivery.LimitedNumber,
                UniqueSeries = delivery.LimitedStack
            }));

        public InventoryItem? Cancel(int owner = 7, IDatabase? source = null) => (source == null ? Store : new MarketplaceCancellationStore(source)).Cancel(owner, 1, _ => Definition, delivery => new Item
        {
            Id = delivery.FurniId,
            OwnerId = (uint)owner,
            Definition = delivery.Definition,
            ExtraData = FurniExtraData.Load(delivery.Definition, delivery.ExtraData, keepLegacy: true),
            UniqueNumber = delivery.LimitedNumber,
            UniqueSeries = delivery.LimitedStack
        }.ToInventoryItem()!);

        public void Dispose()
        {
            Connection.Dispose();
            _admin.Execute($"DROP DATABASE IF EXISTS `{_schema}`");
            _admin.Dispose();
        }
    }
}
