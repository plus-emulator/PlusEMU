using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCoreServer;
using Plus.Communication.Flash;
using Plus.Communication.Nitro;
using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class LoginDeadlineTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 11, 21, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task AFlashConnectionThatDoesNotLogInClosesAtTheDeadline()
    {
        using var server = new ObservedFlashServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            _clock.Advance(GameClient.LoginDeadline - TimeSpan.FromSeconds(1));
            Assert.True(await socket.StaysOpen());

            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(await socket.IsClosed());
        }
        finally {
            server.Stop();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWebSocketConnectionThatDoesNotLogInClosesAtTheDeadline(bool upgraded)
    {
        using var server = new ObservedNitroServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            if (upgraded) {
                await socket.Upgrade();
            }

            _clock.Advance(GameClient.LoginDeadline - TimeSpan.FromSeconds(1));
            Assert.True(await socket.StaysOpen());

            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.True(await socket.IsClosed());
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task ALoggedInFlashConnectionStaysOpenPastTheDeadline()
    {
        using var server = new RecordingTcpServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var client = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(client.TryAttach(new Habbo { Id = 1, Username = "loaded" }, () => { }));

            _clock.Advance(GameClient.LoginDeadline + TimeSpan.FromMinutes(5));

            Assert.True(await socket.StaysOpen());
            Assert.False(client.Closed.IsCancellationRequested);
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task ALoggedInWebSocketConnectionStaysOpenPastTheDeadline()
    {
        using var server = new RecordingWsServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Upgrade();
            var client = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(client.TryAttach(new Habbo { Id = 1, Username = "loaded" }, () => { }));

            _clock.Advance(GameClient.LoginDeadline + TimeSpan.FromMinutes(5));

            Assert.True(await socket.StaysOpen());
            Assert.False(client.Closed.IsCancellationRequested);
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task ALoginThatStartedBeforeTheDeadlineFinishesAfterIt()
    {
        using var server = new RecordingTcpServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var session = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var load = new TaskCompletionSource<Habbo?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var clients = new SharedTestClients();
            var login = SlowLogin(clients, load.Task).AuthenticateUsingSSO(session, "ticket-for-user-seven");

            _clock.Advance(GameClient.LoginDeadline + TimeSpan.FromSeconds(1));
            load.SetResult(LoadedHabbo(clients));

            Assert.Null(await login.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await socket.StaysOpen());
            Assert.False(session.Closed.IsCancellationRequested);
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task ALoginThatFailsAfterTheDeadlinePassedClosesTheConnection()
    {
        using var server = new RecordingTcpServer(_clock);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var session = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var load = new TaskCompletionSource<Habbo?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var login = SlowLogin(new SharedTestClients(), load.Task).AuthenticateUsingSSO(session, "ticket-for-user-seven");

            _clock.Advance(GameClient.LoginDeadline + TimeSpan.FromSeconds(1));
            load.SetResult(null);

            Assert.NotNull(await login.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(await socket.IsClosed());
        }
        finally {
            server.Stop();
        }
    }

    private static Authenticator SlowLogin(SharedTestClients clients, Task<Habbo?> load)
    {
        var tickets = CatalogSnapshotTestSupport.Proxy<ISsoTicketStore>((name, _) => name == nameof(ISsoTicketStore.Consume)
            ? Task.FromResult<int?>(7)
            : throw new NotSupportedException(name));
        var users = CatalogSnapshotTestSupport.Proxy<IUserDataFactory>((name, _) => name == nameof(IUserDataFactory.Create) ? load : throw new NotSupportedException(name));

        return new Authenticator([], clients, users, tickets, new AccountSessionGate(), NoPersistence, TestLoginThrottle.Off);
    }

    private static Habbo LoadedHabbo(SharedTestClients clients)
    {
        var habbo = new Habbo
        {
            Id = 7,
            Username = "seven",
            Access = UserAccess.Empty,
            SessionStartedAt = DateTimeOffset.UtcNow,
            HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, ""),
            Persistence = NoPersistence
        };
        habbo.SetHotelServices(clients, TestRoomManager.Unused);

        return habbo;
    }

    private static IUserPersistenceService NoPersistence => CatalogSnapshotTestSupport.Proxy<IUserPersistenceService>((_, _) => null);

    private static IRevisionsCache NoRevisions => CatalogSnapshotTestSupport.Proxy<IRevisionsCache>((_, _) => null);

    private static IPacketManager NoPackets => CatalogSnapshotTestSupport.Proxy<IPacketManager>((name, _) => throw new NotSupportedException(name));

    private static FlashGameClient NewClient() => new(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient) { Revision = new Revision() };

    private sealed class ObservedFlashServer(TimeProvider clock) : FlashServer(Options.Create(new FlashServerConfiguration { Hostname = "127.0.0.1", Port = 0 }),
        new FlashClientFactory(new FlashPacketFactory(), NoRevisions, TestLogging.GameClient, clock), NoPackets, [], [], NullLogger<FlashServer>.Instance)
    {
        public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override void OnConnected(TcpSession session)
        {
            base.OnConnected(session);
            Connected.TrySetResult();
        }
    }

    private sealed class ObservedNitroServer(TimeProvider clock) : NitroServer(Options.Create(new NitroServerConfiguration { Hostname = "127.0.0.1", Port = 0 }),
        new NitroClientFactory(new FlashPacketFactory(), NoRevisions, TestLogging.GameClient, clock), NoPackets, [], [], NullLogger<NitroServer>.Instance)
    {
        public TaskCompletionSource Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override void OnConnected(TcpSession session)
        {
            base.OnConnected(session);
            Connected.TrySetResult();
        }
    }

    private sealed class RecordingTcpServer(TimeProvider clock) : TcpServer(System.Net.IPAddress.Loopback, 0)
    {
        private GameClient? _client;

        public TaskCompletionSource<GameClient> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override TcpSession CreateSession()
        {
            _client = NewClient();

            return new TcpSessionProxy(this, _client, clock);
        }

        protected override void OnConnected(TcpSession session) => Connected.TrySetResult(_client!);
    }

    private sealed class RecordingWsServer(TimeProvider clock) : WsServer(System.Net.IPAddress.Loopback, 0)
    {
        private GameClient? _client;

        public TaskCompletionSource<GameClient> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override WsSession CreateSession()
        {
            _client = NewClient();

            return new WsSessionProxy(this, _client, clock: clock);
        }

        protected override void OnConnected(TcpSession session) => Connected.TrySetResult(_client!);
    }
}
