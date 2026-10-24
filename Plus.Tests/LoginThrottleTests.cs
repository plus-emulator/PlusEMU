using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Plus.Communication.Abstractions;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class LoginThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly ClientAddress Visitor = new(IPAddress.Parse("203.0.113.7"), true);
    private static readonly ClientAddress OtherVisitor = new(IPAddress.Parse("203.0.113.8"), true);

    [Fact]
    public void MissingSectionUsesTheDefaults()
    {
        var options = Bind(new());

        Assert.Equal(10, options.MaxFailuresPerIp);
        Assert.Equal(5, options.WindowMinutes);
        Assert.Equal(10, options.BlockMinutes);
    }

    [Fact]
    public void ConfiguredValuesOverrideTheDefaults()
    {
        var options = Bind(new()
        {
            ["LoginThrottle:MaxFailuresPerIp"] = "3",
            ["LoginThrottle:WindowMinutes"] = "1",
            ["LoginThrottle:BlockMinutes"] = "30"
        });

        Assert.Equal(3, options.MaxFailuresPerIp);
        Assert.Equal(1, options.WindowMinutes);
        Assert.Equal(30, options.BlockMinutes);
    }

    [Fact]
    public void TheLastAllowedFailureBlocksOnlyThatAddress()
    {
        var (throttle, _) = Throttle();

        throttle.RecordFailure(Visitor);
        throttle.RecordFailure(Visitor);
        Assert.False(throttle.IsBlocked(Visitor));

        throttle.RecordFailure(Visitor);

        Assert.True(throttle.IsBlocked(Visitor));
        Assert.False(throttle.IsBlocked(OtherVisitor));
    }

    [Fact]
    public void FailuresOlderThanTheWindowNoLongerCount()
    {
        var (throttle, clock) = Throttle();

        throttle.RecordFailure(Visitor);
        throttle.RecordFailure(Visitor);
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        throttle.RecordFailure(Visitor);

        Assert.False(throttle.IsBlocked(Visitor));

        throttle.RecordFailure(Visitor);
        throttle.RecordFailure(Visitor);

        Assert.True(throttle.IsBlocked(Visitor));
    }

    [Fact]
    public void ABlockEndsAfterTheBlockTime()
    {
        var (throttle, clock) = Throttle();

        for (var i = 0; i < 3; i++) {
            throttle.RecordFailure(Visitor);
        }

        clock.Advance(TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        Assert.True(throttle.IsBlocked(Visitor));

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(throttle.IsBlocked(Visitor));

        throttle.RecordFailure(Visitor);
        Assert.False(throttle.IsBlocked(Visitor));
    }

    [Fact]
    public void ConnectionsWithoutAVisitorAddressAreNeverBlocked()
    {
        var (throttle, _) = Throttle();
        var proxy = new ClientAddress(IPAddress.Loopback, false);

        for (var i = 0; i < 20; i++) {
            throttle.RecordFailure(proxy);
            throttle.RecordFailure(null);
        }

        Assert.False(throttle.IsBlocked(proxy));
        Assert.False(throttle.IsBlocked(null));
        Assert.Equal(0, ((LoginThrottle)throttle).TrackedAddresses);
    }

    [Theory]
    [InlineData(0, 5, 10)]
    [InlineData(3, 0, 10)]
    [InlineData(3, 5, 0)]
    public void ZeroTurnsTheThrottleOff(int maxFailures, int windowMinutes, int blockMinutes)
    {
        var (throttle, _) = Throttle(new() { MaxFailuresPerIp = maxFailures, WindowMinutes = windowMinutes, BlockMinutes = blockMinutes });

        for (var i = 0; i < 20; i++) {
            throttle.RecordFailure(Visitor);
        }

        Assert.False(throttle.IsBlocked(Visitor));
    }

    [Fact]
    public void TheTableStaysWithinItsCapacityAndKeepsBlockedAddresses()
    {
        var clock = new FakeTimeProvider(Start);
        var throttle = new LoginThrottle(Options.Create(new LoginThrottleConfiguration { MaxFailuresPerIp = 3, WindowMinutes = 5, BlockMinutes = 10 }), clock,
            TestLogging.For<LoginThrottle>(), capacity: 2);
        var blocked = Address("198.51.100.1");

        for (var i = 0; i < 3; i++) {
            throttle.RecordFailure(blocked);
        }

        throttle.RecordFailure(Address("198.51.100.2"));

        for (var i = 3; i < 50; i++) {
            throttle.RecordFailure(Address($"198.51.100.{i}"));
        }

        Assert.True(throttle.TrackedAddresses <= 2);
        Assert.True(throttle.IsBlocked(blocked));

        clock.Advance(TimeSpan.FromMinutes(6));
        var late = Address("198.51.100.200");

        for (var i = 0; i < 3; i++) {
            throttle.RecordFailure(late);
        }

        Assert.True(throttle.TrackedAddresses <= 2);
        Assert.True(throttle.IsBlocked(late));
        Assert.True(throttle.IsBlocked(blocked));
    }

    [Fact]
    public async Task FailedTicketsBlockTheAddressButSuccessfulLoginsNeitherCountNorReset()
    {
        var (throttle, clock) = Throttle();
        var login = new LoginWorld(throttle);

        Assert.Equal(AuthenticationError.NoAccountFound, await login.Attempt(Visitor, "wrong-ticket-0001"));
        Assert.Equal(AuthenticationError.NoAccountFound, await login.Attempt(Visitor, "wrong-ticket-0002"));
        Assert.Null(await login.Attempt(Visitor, "good-ticket-00007"));
        Assert.Equal(AuthenticationError.NoAccountFound, await login.Attempt(Visitor, "wrong-ticket-0003"));

        var consumed = login.Consumed;
        Assert.Equal(AuthenticationError.TooManyAttempts, await login.Attempt(Visitor, "good-ticket-00008"));
        Assert.Equal(consumed, login.Consumed);

        Assert.Null(await login.Attempt(OtherVisitor, "good-ticket-00009"));

        for (var i = 0; i < 5; i++) {
            Assert.Equal(AuthenticationError.TooManyAttempts, await login.Attempt(Visitor, "wrong-ticket-0004"));
        }

        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Null(await login.Attempt(Visitor, "good-ticket-00010"));
    }

    [Fact]
    public async Task ALoginWithoutAVisitorAddressIsNeverThrottled()
    {
        var (throttle, _) = Throttle();
        var login = new LoginWorld(throttle);

        for (var i = 0; i < 5; i++) {
            Assert.Equal(AuthenticationError.NoAccountFound, await login.Attempt(new ClientAddress(IPAddress.Loopback, false), "wrong-ticket-0001"));
            Assert.Equal(AuthenticationError.NoAccountFound, await login.Attempt(null, "wrong-ticket-0002"));
        }

        Assert.Null(await login.Attempt(new ClientAddress(IPAddress.Loopback, false), "good-ticket-00007"));
    }

    private static ClientAddress Address(string address) => new(IPAddress.Parse(address), true);

    private static (ILoginThrottle Throttle, FakeTimeProvider Clock) Throttle(LoginThrottleConfiguration? configuration = null)
    {
        var clock = new FakeTimeProvider(Start);

        return (new LoginThrottle(Options.Create(configuration ?? new LoginThrottleConfiguration { MaxFailuresPerIp = 3, WindowMinutes = 5, BlockMinutes = 10 }), clock,
            TestLogging.For<LoginThrottle>()), clock);
    }

    private static LoginThrottleConfiguration Bind(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        Program.AddConfiguration<LoginThrottleConfiguration>(services, new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("LoginThrottle"));
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<LoginThrottleConfiguration>>().Value;
    }

    private sealed class LoginWorld
    {
        private readonly Authenticator _authenticator;
        private int _consumed;

        public LoginWorld(ILoginThrottle throttle)
        {
            var clients = new SharedTestClients();
            var persistence = CatalogSnapshotTestSupport.Proxy<IUserPersistenceService>((_, _) => null);
            var tickets = CatalogSnapshotTestSupport.Proxy<ISsoTicketStore>((name, args) =>
            {
                if (name != nameof(ISsoTicketStore.Consume)) {
                    throw new NotSupportedException(name);
                }

                Interlocked.Increment(ref _consumed);
                var ticket = (string)args[0]!;

                return Task.FromResult<int?>(ticket.StartsWith("good-ticket-", StringComparison.Ordinal) ? int.Parse(ticket["good-ticket-".Length..]) : null);
            });
            var users = CatalogSnapshotTestSupport.Proxy<IUserDataFactory>((name, args) =>
            {
                if (name != nameof(IUserDataFactory.Create)) {
                    throw new NotSupportedException(name);
                }

                var id = (int)args[0]!;
                var habbo = new Habbo
                {
                    Id = id,
                    Username = $"user{id}",
                    Access = UserAccess.Empty,
                    SessionStartedAt = DateTimeOffset.UtcNow,
                    HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0),
                    Persistence = persistence
                };
                habbo.SetHotelServices(clients, TestRoomManager.Unused);

                return Task.FromResult<Habbo?>(habbo);
            });
            _authenticator = new Authenticator([], clients, users, tickets, new AccountSessionGate(), persistence, throttle);
        }

        public int Consumed => Volatile.Read(ref _consumed);

        public Task<AuthenticationError?> Attempt(ClientAddress? address, string ticket)
        {
            var (session, _) = HabbiconTestSupport.Client(null!);
            session.RemoteAddress = address;

            return _authenticator.AuthenticateUsingSSO(session, ticket);
        }
    }
}
