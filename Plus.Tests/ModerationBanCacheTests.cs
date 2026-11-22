using System.Data;
using System.Net;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MySqlConnector;
using Plus.Communication.Abstractions;
using Plus.Database;
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
    public Task LoadingBansDeletesOnlyTheExpiredRowOfAValue() => WithBansTable(connection =>
    {
        var active = connection.ExecuteScalar<int>("INSERT INTO bans (bantype, value, reason, expire, added_by) VALUES ('ip', '203.0.113.7', 'active', @expire, 'mod'); SELECT LAST_INSERT_ID();",
            new { expire = Now.AddDays(1).UtcDateTime });
        connection.Execute("INSERT INTO bans (bantype, value, reason, expire, added_by) VALUES ('ip', '203.0.113.7', 'expired', @expire, 'mod')",
            new { expire = Now.AddDays(-1).UtcDateTime });

        DatabaseManager(connection.ConnectionString).ReCacheBans();

        Assert.Equal([active], connection.Query<int>("SELECT id FROM bans"));

        return Task.CompletedTask;
    });

    /// <summary>Runs the test against a bans table in its own schema, dropped afterwards.</summary>
    private static async Task WithBansTable(Func<MySqlConnection, Task> test)
    {
        var root = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        using var server = new MySqlConnection(root);
        server.Open();
        var schema = "moderation_ip_bans_" + Guid.NewGuid().ToString("N");
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            using var connection = new MySqlConnection(new MySqlConnectionStringBuilder(root) { Database = schema }.ConnectionString);
            connection.Open();
            connection.Execute("""
                CREATE TABLE bans (
                    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, bantype ENUM('user','ip','machine') NOT NULL DEFAULT 'user',
                    value VARCHAR(50) NOT NULL, reason TEXT NOT NULL, expire DATETIME(6) NULL, added_by VARCHAR(50) NOT NULL, added_date DATETIME(6) NULL);
                """);
            await test(connection);
        }
        finally {
            server.Execute($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }

    private static ModerationManager DatabaseManager(string connectionString) => new(new SchemaDatabase(connectionString), NullLogger<ModerationManager>.Instance,
        Proxy<ISsoTicketStore>((_, _) => Task.CompletedTask), new SharedTestClients(), new AccountSessionGate(), new FakeTimeProvider(Now));

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
