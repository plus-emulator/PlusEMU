using System.Data;
using System.Net;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MySqlConnector;
using Plus.Communication.Abstractions;
using Plus.Database;
using Plus.Database.Migrator;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;
using Xunit;
using static Plus.Tests.CatalogSnapshotTestSupport;

namespace Plus.Tests;

public sealed class ModerationBanCacheTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task MachineCheckOnABannedUsernameLeavesTheUserBanInForce()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Username, "Bob", "reason", Now.AddDays(1));

        // The database holds no machine row named "Bob", which must not lift the user ban.
        Assert.False(manager.HasBan(ModerationBanType.Machine, "Bob"));

        Assert.True(manager.IsBanned(ModerationBanType.Username, "Bob", out var ban));
        Assert.Equal(ModerationBanType.Username, ban.Type);
    }

    [Fact]
    public async Task UserAndMachineBansWithTheSameValueAreKeptApart()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Machine, "Bob", "machine", Now.AddDays(1));
        await manager.BanUser("mod", ModerationBanType.Username, "Bob", "user", Now.AddDays(2));

        Assert.True(manager.IsBanned(ModerationBanType.Machine, "Bob", out var machine));
        Assert.Equal(ModerationBanType.Machine, machine.Type);
        Assert.Equal(Now.AddDays(1), machine.ExpiresAt);
        Assert.True(manager.IsBanned(ModerationBanType.Username, "Bob", out var user));
        Assert.Equal(ModerationBanType.Username, user.Type);
        Assert.Equal(Now.AddDays(2), user.ExpiresAt);
    }

    [Fact]
    public async Task AMachineBanDoesNotBanTheUsernameWithTheSameValue()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Machine, "Bob", "reason", Now.AddDays(1));

        Assert.False(manager.IsBanned(ModerationBanType.Username, "Bob", out _));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task LoginIsRefusedForABannedUsernameOrABannedRecordedMachine(bool userBanned, bool machineBanned, bool allowed)
    {
        var (session, _) = HabbiconTestSupport.Client(new Habbo { Id = 1 });
        session.RecordMachineId("machine");
        var moderation = Proxy<IModerationManager>((method, args) => method switch
        {
            nameof(IModerationManager.HasBan) => (ModerationBanType)args[0]! switch
            {
                ModerationBanType.Username => userBanned && (string)args[1]! == "bob",
                ModerationBanType.Machine => machineBanned && (string)args[1]! == "machine",
                _ => false
            },
            _ => false
        });
        var users = Proxy<IUserDataFactory>((method, _) => method == nameof(IUserDataFactory.GetUsernameForHabboById) ? Task.FromResult("bob") : null);

        Assert.Equal(allowed, await new BanLoginCheckTask(moderation, users).CanLogin(session, 1));
    }

    [Theory]
    [InlineData("203.0.113.7", true, false)]
    [InlineData("203.0.113.8", true, true)]
    [InlineData("203.0.113.7", false, true)]
    public async Task LoginIsRefusedFromABannedVisitorAddress(string address, bool isVisitor, bool allowed)
    {
        var (session, _) = HabbiconTestSupport.Client(new Habbo { Id = 1 });
        session.RemoteAddress = new ClientAddress(IPAddress.Parse(address), isVisitor);
        var moderation = Proxy<IModerationManager>((method, args) =>
            method == nameof(IModerationManager.HasBan) && (ModerationBanType)args[0]! == ModerationBanType.Ip && (string)args[1]! == "203.0.113.7");
        var users = Proxy<IUserDataFactory>((method, _) => method == nameof(IUserDataFactory.GetUsernameForHabboById) ? Task.FromResult("bob") : null);

        Assert.Equal(allowed, await new BanLoginCheckTask(moderation, users).CanLogin(session, 1));
    }

    [Fact]
    public async Task ANewIpBanIsKnownToTheLoginCheck()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Ip, "203.0.113.7", "reason", Now.AddDays(1));

        Assert.True(manager.IsBanned(ModerationBanType.Ip, "203.0.113.7", out var ban));
        Assert.Equal(ModerationBanType.Ip, ban.Type);
    }

    [RoomComponentDatabaseFact]
    public Task AnActiveIpBanInTheDatabaseRefusesALoginFromThatAddress() => WithBansTable(async connection =>
    {
        connection.Execute("INSERT INTO bans (bantype, value, reason, expire, added_by) VALUES ('ip', '203.0.113.7', 'reason', @expire, 'mod')",
            new { expire = Now.AddDays(1).UtcDateTime });

        var manager = DatabaseManager(connection.ConnectionString);
        manager.ReCacheBans();

        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
        Assert.True(await CanLoginFrom(manager, "203.0.113.8"));
    });

    [Theory]
    [InlineData("2001:db8:0:0::5", "2001:db8::5", true)]
    [InlineData("2001:DB8::5", "2001:db8::5", true)]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7", true)]
    [InlineData("203.0.113.7", "::ffff:203.0.113.7", true)]
    [InlineData("203.0.113.7", "203.0.113.8", false)]
    [InlineData("not an address", "203.0.113.7", false)]
    [InlineData("1234", "0.0.4.210", false)]
    public async Task AnIpBanMatchesItsAddressInAnyWrittenForm(string stored, string visitor, bool banned)
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Ip, stored, "reason", Now.AddDays(1));

        Assert.Equal(banned, manager.IsBanned(ModerationBanType.Ip, visitor, out _));
    }

    [RoomComponentDatabaseFact]
    public Task AnIpBanStoredInAnotherFormRefusesALoginFromThatAddress() => WithBansTable(async connection =>
    {
        connection.Execute("""
            INSERT INTO bans (bantype, value, reason, expire, added_by) VALUES
                ('ip', '2001:db8:0:0::5', 'reason', @expire, 'mod'), ('ip', '::ffff:203.0.113.7', 'reason', @expire, 'mod')
            """, new { expire = Now.AddDays(1).UtcDateTime });

        var manager = DatabaseManager(connection.ConnectionString);
        manager.ReCacheBans();

        Assert.False(await CanLoginFrom(manager, "2001:db8::5"));
        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
        Assert.True(await CanLoginFrom(manager, "203.0.113.8"));
    });

    [RoomComponentDatabaseFact]
    public Task ALoadedIpBanHoldsUntilItsLongestRowExpires() => WithBansTable(async connection =>
    {
        InsertBan(connection, "ip", "203.0.113.7", Now.AddHours(1));
        var year = InsertBan(connection, "ip", "203.0.113.7", Now.AddYears(1));
        var clock = new FakeTimeProvider(Now);
        var manager = DatabaseManager(connection.ConnectionString, clock);
        manager.ReCacheBans();
        clock.Advance(TimeSpan.FromHours(2));

        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
        Assert.Contains(year, connection.Query<int>("SELECT id FROM bans"));
    });

    [RoomComponentDatabaseFact]
    public Task AShorterIpBanDoesNotLiftALongerOneOnTheSameAddress() => ShorterIpBanAfterALongerOne(withIndex: true);

    [RoomComponentDatabaseFact]
    public Task AShorterIpBanDoesNotLiftALongerOneInATableWithoutTheIndex() => ShorterIpBanAfterALongerOne(withIndex: false);

    private static Task ShorterIpBanAfterALongerOne(bool withIndex) => WithBansTable(async connection =>
    {
        var clock = new FakeTimeProvider(Now);
        var manager = DatabaseManager(connection.ConnectionString, clock);
        await manager.BanUser("mod", ModerationBanType.Ip, "203.0.113.7", "year", Now.AddYears(1));
        await manager.BanUser("mod", ModerationBanType.Ip, "203.0.113.7", "hour", Now.AddHours(1));
        clock.Advance(TimeSpan.FromHours(2));

        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
        Assert.Equal(["year"], connection.Query<string>("SELECT reason FROM bans WHERE expire > @now", new { now = clock.GetUtcNow().UtcDateTime }));

        manager.ReCacheBans();

        Assert.Equal(["year"], connection.Query<string>("SELECT reason FROM bans"));
        Assert.False(await CanLoginFrom(manager, "203.0.113.7"));
    }, withIndex);

    [RoomComponentDatabaseFact]
    public Task AnExpiredRowInAnotherFormDoesNotLiftAnActiveIpBan() => WithBansTable(async connection =>
    {
        // Many addresses, so the cache meets an expired form before the active one for some of them.
        var addresses = Enumerable.Range(1, 20).Select(octet => $"203.0.113.{octet}").ToList();

        foreach (var address in addresses) {
            InsertBan(connection, "ip", address, Now.AddHours(1));
            InsertBan(connection, "ip", "::ffff:" + address, Now.AddYears(1));
        }

        var clock = new FakeTimeProvider(Now);
        var manager = DatabaseManager(connection.ConnectionString, clock);
        manager.ReCacheBans();
        clock.Advance(TimeSpan.FromHours(2));

        foreach (var address in addresses) {
            Assert.False(await CanLoginFrom(manager, address), address);
        }
    });

    [RoomComponentDatabaseFact]
    public Task AShorterUserBanDoesNotLiftALongerOne() => WithBansTable(async connection =>
    {
        var clock = new FakeTimeProvider(Now);
        var manager = DatabaseManager(connection.ConnectionString, clock);
        await manager.BanUser("mod", ModerationBanType.Username, "bob", "year", Now.AddYears(1));
        await manager.BanUser("mod", ModerationBanType.Username, "bob", "hour", Now.AddHours(1));
        clock.Advance(TimeSpan.FromHours(2));

        Assert.False(await CanLoginFrom(manager, "198.51.100.1"));
        Assert.Equal(2, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM bans"));
    });

    [Theory]
    [InlineData(" 203.0.113.7", "203.0.113.7", true)]
    [InlineData("203.0.113.7 ", "203.0.113.7", true)]
    [InlineData("192.168.010.001", "192.168.8.1", false)]
    [InlineData("192.168.010.001", "192.168.10.1", false)]
    public async Task AStoredIpBanIgnoresSpacesAndNeverReadsAnOctet(string stored, string visitor, bool banned)
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Ip, stored, "reason", Now.AddDays(1));

        Assert.Equal(banned, manager.IsBanned(ModerationBanType.Ip, visitor, out _));
    }

    private static int InsertBan(MySqlConnection connection, string type, string value, DateTimeOffset expire) => connection.ExecuteScalar<int>(
        "INSERT INTO bans (bantype, value, reason, expire, added_by) VALUES (@type, @value, 'reason', @expire, 'mod'); SELECT LAST_INSERT_ID();",
        new { type, value, expire = expire.UtcDateTime });

    /// <summary>Runs the test against the fresh install bans table, optionally without its index, in its own schema, dropped afterwards.</summary>
    private static async Task WithBansTable(Func<MySqlConnection, Task> test, bool withIndex = true)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        server.Open();
        var schema = "moderation_ip_bans_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString);
            connection.Open();
            connection.Execute(SqlScript.Split(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")))
                .Single(statement => statement.Contains("CREATE TABLE `bans` (", StringComparison.Ordinal)));

            if (!withIndex) {
                connection.Execute("ALTER TABLE bans DROP INDEX type_value_expire");
            }

            connection.Execute("CREATE TABLE users (id INT NOT NULL PRIMARY KEY, username VARCHAR(50) NOT NULL, ip_last VARCHAR(45) NOT NULL)");
            await test(connection);
        }
        finally {
            server.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static ModerationManager DatabaseManager(string connectionString, TimeProvider? clock = null) => new(new SchemaDatabase(connectionString),
        NullLogger<ModerationManager>.Instance, Proxy<ISsoTicketStore>((_, _) => Task.CompletedTask), new SharedTestClients(), new AccountSessionGate(),
        clock ?? new FakeTimeProvider(Now));

    private static Task<bool> CanLoginFrom(IModerationManager manager, string address)
    {
        var users = Proxy<IUserDataFactory>((method, _) => method == nameof(IUserDataFactory.GetUsernameForHabboById) ? Task.FromResult("bob") : null);
        var (session, _) = HabbiconTestSupport.Client(new Habbo { Id = 1 });
        session.RemoteAddress = new ClientAddress(IPAddress.Parse(address), true);

        return new BanLoginCheckTask(manager, users).CanLogin(session, 1);
    }

    private sealed class SchemaDatabase(string connectionString) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new MySqlConnection(connectionString);
    }

    private static ModerationManager Manager() => new(
        Proxy<IDatabase>((method, _) => method == nameof(IDatabase.Connection) ? new PlacedFurniRoomTests.NoOpConnection() : null),
        NullLogger<ModerationManager>.Instance, Proxy<ISsoTicketStore>((_, _) => Task.CompletedTask), new SharedTestClients(),
        new AccountSessionGate(), new FakeTimeProvider(Now));
}
