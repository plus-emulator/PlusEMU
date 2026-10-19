using System.Buffers.Binary;
using System.Data;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Communication.Packets.Incoming.Catalog;
using Plus.Communication.Packets.Incoming.Misc;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Catalog;
using Plus.Communication.Packets.Outgoing.Misc;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.Catalog.Claims;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Inventory;
using Plus.Utilities;
using Xunit;

namespace Plus.Tests;

public sealed class ClaimProductServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private const string Dragon = ClaimProductCatalog.CursedDragonAdoption;

    [Fact]
    public void ACursedDragonIsClaimedOnceWithThePickedPalette()
    {
        var (service, store, _) = Service();
        var habbo = User();

        var first = service.Claim(habbo, Dragon, 1);
        Assert.Equal(ClaimProductResult.Claimed, first.Result);
        var pet = Assert.Single(habbo.Inventory!.Pets.Pets.Values);
        Assert.Equal((37, "1", "ffffff", ClaimProductCatalog.DefaultCursedDragonName, 0u, false),
            (pet.Type, pet.Race, pet.Color, pet.Name, pet.RoomId, pet.PlacedInRoom));
        Assert.Equal((7, "johndoe", Dragon, (int?)1, Now), (store.Requests[0].UserId, store.Requests[0].OwnerName,
            store.Requests[0].ClaimId, store.Requests[0].Choice, store.Requests[0].ClaimedAt));
        Assert.True(service.HasClaimed(habbo.Id, Dragon));

        Assert.Equal(ClaimProductResult.AlreadyClaimed, service.Claim(habbo, Dragon, 0).Result);
        Assert.Single(habbo.Inventory.Pets.Pets);
        Assert.Single(store.Requests);
    }

    [Fact]
    public void ARaceTheStoreLosesIsReportedAsAlreadyClaimed()
    {
        var (service, store, _) = Service();
        store.Lose = true;

        Assert.Equal(ClaimProductResult.AlreadyClaimed, service.Claim(User(), Dragon, 0).Result);
    }

    [Fact]
    public void UnknownClaimsAreNotFoundAndNeverClaimed()
    {
        var (service, store, _) = Service();
        var habbo = User();

        Assert.Equal(ClaimProductResult.NotFound, service.Claim(habbo, "chests", 0).Result);
        Assert.Equal(ClaimProductResult.NotFound, service.Claim(habbo, "", 0).Result);
        Assert.Equal(ClaimProductResult.NotFound, service.Claim(habbo, new string('a', 65), 0).Result);
        Assert.False(service.HasClaimed(habbo.Id, "chests"));
        Assert.Empty(store.Requests);
    }

    [Theory]
    [InlineData("claim_product.cursed_dragon_adoption.enabled", "0")]
    [InlineData("claim_product.cursed_dragon_adoption.expires_at", "2026-11-01 12:00")]
    [InlineData("claim_product.cursed_dragon_adoption.expires_at", "2026-10-31 23:59:59")]
    [InlineData("claim_product.cursed_dragon_adoption.expires_at", "not a date")]
    public void AClosedClaimIsNotActiveUnlessItWasAlreadyClaimed(string key, string value)
    {
        var (service, store, _) = Service(new() { [key] = value });
        var habbo = User();

        Assert.Equal(ClaimProductResult.NotActive, service.Claim(habbo, Dragon, 0).Result);
        Assert.Empty(store.Requests);
        Assert.Empty(habbo.Inventory!.Pets.Pets);

        store.Claimed.Add((habbo.Id, Dragon));
        Assert.Equal(ClaimProductResult.AlreadyClaimed, service.Claim(habbo, Dragon, 0).Result);
    }

    [Fact]
    public void AClaimBeforeItsExpiryAndANamedDragonFollowTheSettings()
    {
        var (service, _, _) = Service(new()
        {
            ["claim_product.cursed_dragon_adoption.enabled"] = "1",
            ["claim_product.cursed_dragon_adoption.expires_at"] = "2026-11-01 12:01",
            ["claim_product.cursed_dragon_adoption.pet_name"] = "Smaug"
        });
        var habbo = User();

        Assert.Equal(ClaimProductResult.Claimed, service.Claim(habbo, Dragon, 0).Result);
        Assert.Equal("Smaug", Assert.Single(habbo.Inventory!.Pets.Pets.Values).Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    [InlineData(2)]
    public void AChoiceTheDragonDoesNotOfferFails(int? choice)
    {
        var (service, store, _) = Service();

        Assert.Equal(ClaimProductResult.Failed, service.Claim(User(), Dragon, choice).Result);
        Assert.Empty(store.Requests);
    }

    [Fact]
    public void AFailedDeliveryLeavesTheClaimOpen()
    {
        var (service, store, _) = Service();
        var habbo = User();
        store.Fail = true;

        Assert.Equal(ClaimProductResult.Failed, service.Claim(habbo, Dragon, 0).Result);
        Assert.Empty(habbo.Inventory!.Pets.Pets);
        Assert.False(service.HasClaimed(habbo.Id, Dragon));

        store.Fail = false;
        Assert.Equal(ClaimProductResult.Claimed, service.Claim(habbo, Dragon, 0).Result);
    }

    [Fact]
    public async Task HandlersReadTheOptionalChoiceAndAnswerWithTheResultAfterThePetPackets()
    {
        var (service, store, _) = Service();
        var habbo = User();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;

        await new HasClaimedProductEvent(service).Parse(client, HabbiconTestSupport.Incoming(Dragon));
        Assert.Equal(ServerPacketHeader.HasClaimedProductResponseComposer, Assert.Single(sent).Header);
        Assert.Equal(Concat(Str(Dragon), [0]), sent[0].Payload);
        sent.Clear();

        // No int: the client leaves the choice out when it is -1.
        await new ClaimProductEvent(service).Parse(client, HabbiconTestSupport.Incoming(Dragon));
        Assert.Equal(ServerPacketHeader.ClaimProductResultComposer, Assert.Single(sent).Header);
        Assert.Equal(Concat(Str(Dragon), Int(4)), sent[0].Payload);
        sent.Clear();

        await new ClaimProductEvent(service).Parse(client, HabbiconTestSupport.Incoming(Dragon, 1));
        Assert.Equal([ServerPacketHeader.FurniListNotificationComposer, ServerPacketHeader.PetInventoryComposer, ServerPacketHeader.ClaimProductResultComposer],
            sent.Select(packet => (uint)packet.Header));
        Assert.Equal(Concat(Str(Dragon), Int(0)), sent[^1].Payload);
        Assert.Equal((int?)1, Assert.Single(store.Requests).Choice);
        sent.Clear();

        await new HasClaimedProductEvent(service).Parse(client, HabbiconTestSupport.Incoming(Dragon));
        Assert.Equal(Concat(Str(Dragon), [1]), Assert.Single(sent).Payload);
    }

    [Fact]
    public void ComposersWriteTheirFieldsInHabboOrder()
    {
        var has = new HabbiconTestSupport.RecordingPacket();
        new HasClaimedProductResponseComposer(Dragon, true).Compose(has);
        Assert.Equal(new object[] { Dragon, true }, has.Writes);

        var result = new HabbiconTestSupport.RecordingPacket();
        new ClaimProductResultComposer(Dragon, 3).Compose(result);
        Assert.Equal(new object[] { Dragon, 3 }, result.Writes);

        var seconds = new HabbiconTestSupport.RecordingPacket();
        new SecondsUntilComposer("2026-11-05 23:59", 42).Compose(seconds);
        Assert.Equal(new object[] { "2026-11-05 23:59", 42 }, seconds.Writes);
    }

    [Theory]
    [InlineData("2026-11-01 12:01", 60)]
    [InlineData("2026-11-01 12:00:30", 30)]
    [InlineData("2026-11-01 12:00", 0)]
    [InlineData("2026-11-01 11:59", -60)]
    [InlineData(" 2026-11-02 ", 43200)]
    [InlineData("tomorrow", 0)]
    [InlineData("", 0)]
    public void SecondsUntilReadsServerLocalTimes(string text, int expected)
    {
        Assert.Equal(expected, ServerTimeText.SecondsUntil(text, Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void SecondsUntilUsesTheServerTimeZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus+2", TimeSpan.FromHours(2), "plus+2", "plus+2");

        Assert.Equal(60, ServerTimeText.SecondsUntil("2026-11-01 14:01", Now, zone));
    }

    [Fact]
    public async Task GetSecondsUntilEchoesTheTimeWithTheSecondsLeft()
    {
        var habbo = User();
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        var clock = new ZonedClock(Now, TimeZoneInfo.Utc);

        await new GetSecondsUntilEvent(clock).Parse(client, HabbiconTestSupport.Incoming("2026-11-01 13:00"));

        Assert.Equal(ServerPacketHeader.SecondsUntilComposer, Assert.Single(sent).Header);
        Assert.Equal(Concat(Str("2026-11-01 13:00"), Int(3600)), sent[0].Payload);
    }

    [ClaimProductDatabaseFact]
    public void StoreKeepsOneClaimPerUserAndCommitsThePetWithIt()
    {
        using var root = new MySqlConnection(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"));
        root.Open();
        var schema = "claims_" + Guid.NewGuid().ToString("N");
        root.Execute($"CREATE DATABASE `{schema}`");

        try {
            var cs = new MySqlConnectionStringBuilder(root.ConnectionString) { Database = schema }.ConnectionString;
            using var connection = new MySqlConnection(cs);
            connection.Open();
            connection.Execute("""
                CREATE TABLE users (id INT PRIMARY KEY) ENGINE=InnoDB;
                CREATE TABLE bots_pet_responses (pet_id VARCHAR(255) PRIMARY KEY, responses TEXT NOT NULL) ENGINE=InnoDB;
                CREATE TABLE bots (id INT AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, room_id INT UNSIGNED NOT NULL,
                    name VARCHAR(50) NOT NULL, motto VARCHAR(50) NOT NULL, look VARCHAR(100) NOT NULL,
                    x INT NOT NULL, y INT NOT NULL, z DOUBLE NOT NULL, ai_type VARCHAR(20) NOT NULL) ENGINE=InnoDB;
                CREATE TABLE bots_petdata (id INT PRIMARY KEY, type INT NOT NULL, race VARCHAR(20) NOT NULL, color VARCHAR(20) NOT NULL,
                    experience INT NOT NULL, energy INT NOT NULL, nutrition INT NOT NULL, respect INT NOT NULL,
                    createstamp DATETIME(6) NULL, have_saddle INT NOT NULL, anyone_ride INT NOT NULL,
                    hairdye INT NOT NULL, pethair INT NOT NULL, gnome_clothing VARCHAR(100) NOT NULL) ENGINE=InnoDB;
                INSERT INTO users VALUES (7);
                """);
            var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/95_ClaimedProducts.sql"));
            connection.Execute(migration);
            connection.Execute(migration);
            Assert.Equal("Grrr;", connection.QuerySingle<string>("SELECT responses FROM bots_pet_responses WHERE pet_id = 'speech.pet37'"));

            var store = new ClaimProductStore(new ProbeDatabase(cs), NullLogger<ClaimProductStore>.Instance);
            var request = new ClaimProductRequest(7, "johndoe", Dragon, 1, new PetClaimReward(37, "1", "ffffff", "Dragon"), Now);

            connection.Execute("CREATE TRIGGER fail_pet BEFORE INSERT ON bots_petdata FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced pet failure'");
            Assert.Equal(ClaimProductResult.Failed, store.Claim(request).Result);
            Assert.False(store.HasClaimed(7, Dragon));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bots"));
            connection.Execute("DROP TRIGGER fail_pet");

            var claimed = store.Claim(request);
            Assert.Equal(ClaimProductResult.Claimed, claimed.Result);
            Assert.Equal("37 1 ffffff 2 2 -1 0 3 -1 0", Assert.IsType<Pet>(claimed.Grant?.Pet).Look);
            Assert.True(store.HasClaimed(7, Dragon));
            Assert.Equal(1, connection.QuerySingle<int>("SELECT choice FROM user_claimed_products WHERE user_id = 7"));

            Assert.Equal(ClaimProductResult.AlreadyClaimed, store.Claim(request with { Choice = 0 }).Result);
            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bots"));
        }
        finally {
            root.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private static (ClaimProductService Service, FakeStore Store, FakeSettings Settings) Service(Dictionary<string, string>? settings = null)
    {
        var store = new FakeStore();
        var values = new FakeSettings(settings ?? new());
        var clock = new ZonedClock(Now, TimeZoneInfo.Utc);
        var service = new ClaimProductService(new ClaimProductCatalog(values, clock), store, new AccountSessionGate(), clock,
            TestLogging.For<ClaimProductService>());

        return (service, store, values);
    }

    private static Habbo User() => new()
    {
        Id = 7,
        Username = "johndoe",
        Access = UserAccess.Empty,
        Inventory = new InventoryComponent { Badges = new([]), Furniture = new([], []) }
    };

    private static byte[] Str(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var result = new byte[2 + bytes.Length];
        BinaryPrimitives.WriteUInt16BigEndian(result, (ushort)bytes.Length);
        bytes.CopyTo(result, 2);

        return result;
    }

    private static byte[] Int(int value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(bytes, value);

        return bytes;
    }

    private static byte[] Concat(byte[] first, byte[] second) => [.. first, .. second];

    private sealed class FakeStore : IClaimProductStore
    {
        private int _nextPetId = 100;
        public HashSet<(int UserId, string ClaimId)> Claimed { get; } = [];
        public List<ClaimProductRequest> Requests { get; } = [];
        public bool Fail;
        public bool Lose;

        public bool HasClaimed(int userId, string claimId) => Claimed.Contains((userId, claimId));

        public ClaimProductStoreResult Claim(ClaimProductRequest request)
        {
            Requests.Add(request);

            if (Lose || !Claimed.Add((request.UserId, request.ClaimId))) {
                return new(ClaimProductResult.AlreadyClaimed);
            }

            if (Fail) {
                Claimed.Remove((request.UserId, request.ClaimId));

                return new(ClaimProductResult.Failed);
            }

            var reward = Assert.IsType<PetClaimReward>(request.Reward);

            return new(ClaimProductResult.Claimed, new(new Pet(_nextPetId++, request.UserId, 5, reward.Name, reward.Type, reward.Race,
                reward.Colour, 0, 100, 100, 0, request.ClaimedAt, 0, 0, 0, 0, 0, 0, -1, "-1", request.OwnerName)));
        }
    }

    private sealed class FakeSettings(Dictionary<string, string> values) : ISettingsManager
    {
        public string TryGetValue(string value) => values.GetValueOrDefault(value, "0");
        public string? GetOptionalValue(string key) => values.GetValueOrDefault(key);
        public Task Reload() => Task.CompletedTask;
    }

    private sealed class ZonedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => zone;
    }

    private sealed class ProbeDatabase(string connectionString) : IDatabase
    {
        public IDbConnection Connection() => new MySqlConnection(connectionString);
        public bool IsConnected() => true;
    }
}

public sealed class ClaimProductDatabaseFactAttribute : FactAttribute
{
    public ClaimProductDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE") is null) {
            Skip = "Opt-in isolated claim product MariaDB probe.";
        }
    }
}
