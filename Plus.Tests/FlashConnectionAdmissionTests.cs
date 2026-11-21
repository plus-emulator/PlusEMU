using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetCoreServer;
using Plus.Communication.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

public sealed class FlashConnectionAdmissionTests
{
    [Fact]
    public async Task TheFlashServerClosesConnectionsOverThePerAddressLimitAndFreesSlotsOnClose()
    {
        // Without trusted proxies a loopback peer counts as a visitor, like any other socket address.
        using var server = Flash(new FlashServerConfiguration { Hostname = "127.0.0.1", Port = 0, TrustedProxies = [], MaxConnectionsPerIp = 2 });
        Assert.True(server.Start());

        try {
            using var first = await GameSocket.Connect(server);
            using var second = await GameSocket.Connect(server);
            Assert.True(await first.StaysOpen());
            Assert.True(await second.StaysOpen());

            using var third = await GameSocket.Connect(server);
            Assert.True(await third.IsClosed());

            first.Dispose();
            Assert.True(await Eventually(async () =>
            {
                using var retry = await GameSocket.Connect(server);

                return await retry.StaysOpen();
            }));
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task TheFlashServerKeepsNothingOfTheConnectionsItRefuses()
    {
        using var server = new TrackingFlashServer(new FlashServerConfiguration { Hostname = "127.0.0.1", Port = 0, TrustedProxies = [], MaxConnectionsPerIp = 1 });
        Assert.True(server.Start());

        try {
            using var held = await GameSocket.Connect(server);
            Assert.True(await held.StaysOpen());

            for (var i = 0; i < 200; i++) {
                using var refused = await GameSocket.Connect(server);
                Assert.True(await refused.IsClosed());
            }

            Assert.True(await Eventually(() =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                return Task.FromResult(server.ConnectedSessions == 1 && server.Alive == 1);
            }), $"{server.Alive} of {server.Created} sessions are still alive");
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task LocalFlashConnectionsAreNotLimitedWhenLoopbackIsATrustedProxy()
    {
        using var server = Flash(new FlashServerConfiguration { Hostname = "127.0.0.1", Port = 0, MaxConnectionsPerIp = 1 });
        Assert.True(server.Start());

        try {
            using var first = await GameSocket.Connect(server);
            using var second = await GameSocket.Connect(server);

            Assert.True(await first.StaysOpen());
            Assert.True(await second.StaysOpen());
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task AFlashSessionKnowsItsSocketAddress()
    {
        var admission = new ConnectionAdmission(new FlashServerConfiguration { TrustedProxies = [] }, TimeProvider.System, NullLogger.Instance);
        using var server = new RecordingTcpServer(admission);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var client = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(new ClientAddress(IPAddress.Loopback, true), client.RemoteAddress);
        }
        finally {
            server.Stop();
        }
    }

    private static FlashServer Flash(FlashServerConfiguration configuration) =>
        new(Options.Create(configuration),
            new FlashClientFactory(new FlashPacketFactory(), CatalogSnapshotTestSupport.Proxy<IRevisionsCache>((_, _) => null), TestLogging.GameClient, TimeProvider.System),
            CatalogSnapshotTestSupport.Proxy<IPacketManager>((name, _) => throw new NotSupportedException(name)), [], [], NullLogger<FlashServer>.Instance);

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

    /// <summary>The real Flash server, with a weak reference to each session so a test can see which ones stay in memory.</summary>
    private sealed class TrackingFlashServer(FlashServerConfiguration configuration) : FlashServer(Options.Create(configuration),
        new FlashClientFactory(new FlashPacketFactory(), CatalogSnapshotTestSupport.Proxy<IRevisionsCache>((_, _) => null), TestLogging.GameClient, TimeProvider.System),
        CatalogSnapshotTestSupport.Proxy<IPacketManager>((name, _) => throw new NotSupportedException(name)), [], [], NullLogger<FlashServer>.Instance)
    {
        private readonly List<WeakReference> _sessions = [];

        public int Created
        {
            get
            {
                lock (_sessions) {
                    return _sessions.Count;
                }
            }
        }

        public int Alive
        {
            get
            {
                lock (_sessions) {
                    return _sessions.Count(session => session.IsAlive);
                }
            }
        }

        protected override TcpSession CreateSession()
        {
            var session = base.CreateSession();

            lock (_sessions) {
                _sessions.Add(new(session));
            }

            return session;
        }
    }

    private sealed class RecordingTcpServer(ConnectionAdmission admission) : TcpServer(IPAddress.Loopback, 0)
    {
        private GameClient? _client;

        public TaskCompletionSource<GameClient> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override TcpSession CreateSession()
        {
            _client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient) { Revision = new Revision() };

            return new TcpSessionProxy(this, _client, admission: admission);
        }

        protected override void OnConnected(TcpSession session) => Connected.TrySetResult(_client!);
    }
}
