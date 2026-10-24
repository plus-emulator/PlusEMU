using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCoreServer;
using Plus.Communication.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Nitro;
using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;
using TcpClient = System.Net.Sockets.TcpClient;

namespace Plus.Tests;

public sealed class ConnectionAdmissionTests
{
    private static readonly IPAddress Visitor = IPAddress.Parse("203.0.113.7");

    [Fact]
    public void TrustedProxyForwardsTheRealIp()
    {
        var address = Admission().Resolve(IPAddress.Loopback, [("X-Real-IP", "203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Fact]
    public void ForgedForwardedForEntriesAreIgnoredForTheLastOne()
    {
        var address = Admission().Resolve(IPAddress.Loopback, [("X-Forwarded-For", "6.6.6.6, 10.0.0.9, 203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Fact]
    public void TheLastForwardedForLineWins()
    {
        var address = Admission().Resolve(IPAddress.Loopback, [("X-Forwarded-For", "6.6.6.6"), ("X-Forwarded-For", "203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Fact]
    public void RealIpWinsOverForwardedFor()
    {
        var address = Admission().Resolve(IPAddress.Loopback, [("X-Forwarded-For", "198.51.100.4"), ("X-Real-IP", "203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Fact]
    public void HeaderNamesIgnoreCase()
    {
        Assert.Equal(new ClientAddress(Visitor, true), Admission().Resolve(IPAddress.Loopback, [("x-real-ip", "203.0.113.7")]));
        Assert.Equal(new ClientAddress(Visitor, true), Admission().Resolve(IPAddress.Loopback, [("x-forwarded-for", "203.0.113.7")]));
    }

    [Fact]
    public void AnUntrustedPeerKeepsItsSocketAddress()
    {
        var peer = IPAddress.Parse("198.51.100.20");

        var address = Admission().Resolve(peer, [("X-Real-IP", "203.0.113.7"), ("X-Forwarded-For", "203.0.113.7")]);

        Assert.Equal(new ClientAddress(peer, true), address);
    }

    [Fact]
    public void IPv6LoopbackIsATrustedProxy()
    {
        Assert.Equal(new ClientAddress(IPAddress.Parse("2001:db8::5"), true), Admission().Resolve(IPAddress.IPv6Loopback, [("X-Real-IP", "2001:db8::5")]));
        Assert.Equal(new ClientAddress(Visitor, true), Admission().Resolve(IPAddress.IPv6Loopback, [("X-Forwarded-For", "203.0.113.7")]));
    }

    [Fact]
    public void AnIPv4MappedLoopbackPeerIsTrustedAndMappedVisitorsAreUnwrapped()
    {
        var address = Admission().Resolve(IPAddress.Parse("::ffff:127.0.0.1"), [("X-Real-IP", "::ffff:203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Theory]
    [InlineData("203.0.113.7:51234")]
    [InlineData(" 203.0.113.7 ")]
    public void AForwardedIPv4AddressMayCarryAPortOrSpaces(string value)
    {
        Assert.Equal(new ClientAddress(Visitor, true), Admission().Resolve(IPAddress.Loopback, [("X-Forwarded-For", value)]));
    }

    [Fact]
    public void AForwardedIPv6AddressMayCarryAPort()
    {
        Assert.Equal(new ClientAddress(IPAddress.Parse("2001:db8::5"), true), Admission().Resolve(IPAddress.Loopback, [("X-Forwarded-For", "[2001:db8::5]:443")]));
    }

    [Theory]
    [InlineData("X-Real-IP", "")]
    [InlineData("X-Real-IP", "unknown")]
    [InlineData("X-Real-IP", "1234")]
    [InlineData("X-Real-IP", "999.1.1.1")]
    [InlineData("X-Forwarded-For", "203.0.113.7, not-an-address")]
    [InlineData("X-Forwarded-For", "203.0.113.7,")]
    [InlineData("X-Forwarded-For", "")]
    public void ABadHeaderFallsBackToTheSocketAddress(string name, string value)
    {
        var address = Admission().Resolve(IPAddress.Loopback, [(name, value)]);

        Assert.Equal(new ClientAddress(IPAddress.Loopback, false), address);
    }

    [Fact]
    public void ABadRealIpFallsBackToForwardedFor()
    {
        var address = Admission().Resolve(IPAddress.Loopback, [("X-Real-IP", "unknown"), ("X-Forwarded-For", "203.0.113.7")]);

        Assert.Equal(new ClientAddress(Visitor, true), address);
    }

    [Fact]
    public void ATrustedProxyWithoutHeadersIsNoVisitor()
    {
        Assert.Equal(new ClientAddress(IPAddress.Loopback, false), Admission().Resolve(IPAddress.Loopback, [("Host", "127.0.0.1")]));
    }

    [Fact]
    public void ConfiguredTrustedProxiesReplaceTheLoopbackDefault()
    {
        var admission = Admission(new NitroServerConfiguration { TrustedProxies = ["10.0.0.2", "not-an-address"] });

        Assert.Equal(new ClientAddress(Visitor, true), admission.Resolve(IPAddress.Parse("10.0.0.2"), [("X-Real-IP", "203.0.113.7")]));
        Assert.Equal(new ClientAddress(IPAddress.Loopback, true), admission.Resolve(IPAddress.Loopback, [("X-Real-IP", "203.0.113.7")]));
    }

    [Fact]
    public void NitroDefaultsKeepLoopbackTrustAndTwentyConnections()
    {
        var nitro = Bind(new() { ["Nitro:Port"] = "2096" });

        Assert.Null(nitro.TrustedProxies);
        Assert.Equal(20, nitro.MaxConnectionsPerIp);
        Assert.Equal(new ClientAddress(Visitor, true), Admission(nitro).Resolve(IPAddress.Loopback, [("X-Real-IP", "203.0.113.7")]));
    }

    [Fact]
    public void NitroBindsTrustedProxiesAndTheConnectionLimit()
    {
        var nitro = Bind(new()
        {
            ["Nitro:TrustedProxies:0"] = "10.0.0.2",
            ["Nitro:MaxConnectionsPerIp"] = "5"
        });

        Assert.Equal(["10.0.0.2"], nitro.TrustedProxies!);
        Assert.Equal(5, nitro.MaxConnectionsPerIp);
    }

    [Fact]
    public void AnAddressGetsOnlyItsLimitOfConnections()
    {
        var admission = Admission(Limit(2));
        var visitor = new ClientAddress(Visitor, true);

        Assert.NotNull(admission.TryAdmit(visitor));
        Assert.NotNull(admission.TryAdmit(visitor));
        Assert.Null(admission.TryAdmit(visitor));
        Assert.NotNull(admission.TryAdmit(new ClientAddress(IPAddress.Parse("203.0.113.8"), true)));
    }

    [Fact]
    public void ClosingAConnectionFreesExactlyOneSlot()
    {
        var admission = Admission(Limit(2));
        var visitor = new ClientAddress(Visitor, true);
        var first = admission.TryAdmit(visitor)!;
        Assert.NotNull(admission.TryAdmit(visitor));

        first.Dispose();
        first.Dispose();

        Assert.NotNull(admission.TryAdmit(visitor));
        Assert.Null(admission.TryAdmit(visitor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ZeroOrLessTurnsTheLimitOff(int limit)
    {
        var admission = Admission(Limit(limit));

        for (var i = 0; i < 50; i++) {
            Assert.NotNull(admission.TryAdmit(new ClientAddress(Visitor, true)));
        }
    }

    [Fact]
    public void AProxyWithoutAVisitorAddressIsNotLimited()
    {
        var admission = Admission(Limit(2));

        for (var i = 0; i < 50; i++) {
            Assert.NotNull(admission.TryAdmit(new ClientAddress(IPAddress.Loopback, false)));
        }
    }

    [Fact]
    public void ARefusalIsLoggedOncePerAddressPerMinute()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));
        var logger = new RecordingLogger();
        var admission = new ConnectionAdmission(Limit(1), clock, logger);
        var visitor = new ClientAddress(Visitor, true);
        var other = new ClientAddress(IPAddress.Parse("203.0.113.8"), true);
        Assert.NotNull(admission.TryAdmit(visitor));
        Assert.NotNull(admission.TryAdmit(other));

        for (var i = 0; i < 5; i++) {
            Assert.Null(admission.TryAdmit(visitor));
        }

        Assert.Single(logger.Warnings);
        Assert.Contains("203.0.113.7", logger.Warnings[0]);

        Assert.Null(admission.TryAdmit(other));
        Assert.Equal(2, logger.Warnings.Count);

        clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Null(admission.TryAdmit(visitor));
        Assert.Equal(2, logger.Warnings.Count);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Null(admission.TryAdmit(visitor));
        Assert.Equal(3, logger.Warnings.Count);
    }

    [Fact]
    public async Task TheGameServerClosesConnectionsOverTheLimitBeforeTheUpgradeAndFreesSlotsOnClose()
    {
        using var server = new NitroServer(Options.Create(new NitroServerConfiguration { Hostname = "127.0.0.1", Port = 0, MaxConnectionsPerIp = 2 }),
            new NitroClientFactory(new FlashPacketFactory(), CatalogSnapshotTestSupport.Proxy<IRevisionsCache>((_, _) => null), TestLogging.GameClient),
            CatalogSnapshotTestSupport.Proxy<IPacketManager>((name, _) => throw new NotSupportedException(name)), [], [], NullLogger<NitroServer>.Instance);
        Assert.True(server.Start());

        try {
            using var first = await Connect(server, "203.0.113.7");
            using var second = await Connect(server, "203.0.113.7");
            Assert.True(first.Upgraded);
            Assert.True(second.Upgraded);

            using var third = await Connect(server, "203.0.113.7");
            Assert.False(third.Upgraded);

            using var otherVisitor = await Connect(server, "203.0.113.8");
            Assert.True(otherVisitor.Upgraded);

            first.Dispose();
            Assert.True(await Eventually(async () =>
            {
                using var retry = await Connect(server, "203.0.113.7");

                return retry.Upgraded;
            }));
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task LocalConnectionsWithoutForwardedHeadersAreNotLimited()
    {
        using var server = new RecordingServer(Admission(Limit(1)));
        Assert.True(server.Start());

        try {
            using var first = await Connect(server, null);
            using var second = await Connect(server, null);

            Assert.True(first.Upgraded);
            Assert.True(second.Upgraded);
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task TheSessionKnowsTheForwardedVisitorAddress()
    {
        using var server = new RecordingServer(Admission());
        Assert.True(server.Start());

        try {
            using var connection = await Connect(server, "203.0.113.7");

            Assert.True(connection.Upgraded);
            Assert.Equal(new ClientAddress(Visitor, true), Assert.Single(server.Clients).RemoteAddress);
        }
        finally {
            server.Stop();
        }
    }

    private static NitroServerConfiguration Limit(int limit) => new() { MaxConnectionsPerIp = limit };

    private static ConnectionAdmission Admission(NitroServerConfiguration? options = null) =>
        new(options ?? new NitroServerConfiguration(), TimeProvider.System, NullLogger.Instance);

    private static NitroServerConfiguration Bind(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        Program.AddConfiguration<NitroServerConfiguration>(services, new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection("Nitro"));
        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<NitroServerConfiguration>>().Value;
    }

    private static async Task<bool> Eventually(Func<Task<bool>> attempt)
    {
        for (var i = 0; i < 50; i++) {
            if (await attempt()) {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    private static async Task<Connection> Connect(WsServer server, string? realIp)
    {
        var tcp = new TcpClient { NoDelay = true };
        await tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)server.Endpoint).Port);
        var stream = tcp.GetStream();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var forwarded = realIp == null ? "" : $"X-Real-IP: {realIp}\r\n";
        var request = "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                      $"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n{forwarded}\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        var response = new StringBuilder();
        var one = new byte[1];

        try {
            while (!response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
                if (await stream.ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(5)) == 0) {
                    break;
                }

                response.Append((char)one[0]);
            }
        }
        catch (IOException) {
        }

        return new(tcp, response.ToString().StartsWith("HTTP/1.1 101", StringComparison.Ordinal));
    }

    private sealed record Connection(TcpClient Tcp, bool Upgraded) : IDisposable
    {
        public void Dispose() => Tcp.Dispose();
    }

    private sealed class RecordingServer(ConnectionAdmission admission) : WsServer(IPAddress.Loopback, 0)
    {
        public ConcurrentQueue<GameClient> Clients { get; } = new();

        protected override WsSession CreateSession()
        {
            var client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient) { Revision = new Revision() };
            Clients.Enqueue(client);

            return new WsSessionProxy(this, client, null, admission);
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) {
                Warnings.Add(formatter(state, exception));
            }
        }
    }
}
