using System.Buffers.Binary;
using System.Net;
using Microsoft.Extensions.Logging;
using Plus.Communication.Abstractions;
using Plus.Communication.Attributes;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Xunit;
using ClientHelloEvent = Plus.Communication.Packets.Incoming.Handshake.ClientHelloEvent;

namespace Plus.Tests;

public sealed class MalformedPacketTests
{
    private static readonly IPAddress Address = IPAddress.Parse("203.0.113.7");

    [Fact]
    public async Task PacketThatEndsBeforeItsFieldsClosesTheConnectionAndLogsNoStackTrace()
    {
        var connection = new Connection(new ClientHelloEvent(new RevisionsCache(), TestLogging.For<ClientHelloEvent>()), null);

        await connection.ReceiveEmptyPacket();

        Assert.True(connection.Client.Closed.IsCancellationRequested);
        var entry = Assert.Single(connection.PacketLog.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(nameof(ClientHelloEvent), entry.Message);
        Assert.Contains(Address.ToString(), entry.Message);
        Assert.DoesNotContain("   at ", entry.Message);
        Assert.Empty(connection.ClientLog.Entries);
    }

    [Fact]
    public async Task MalformedPacketFromALoggedInUserIsSkippedWithOneWarning()
    {
        var connection = new Connection(new ClientHelloEvent(new RevisionsCache(), TestLogging.For<ClientHelloEvent>()), new Habbo { Id = 1, Username = "alice" });

        await connection.ReceiveEmptyPacket();

        Assert.False(connection.Client.Closed.IsCancellationRequested);
        var entry = Assert.Single(connection.PacketLog.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(nameof(ClientHelloEvent), entry.Message);
        Assert.Contains("alice", entry.Message);
        Assert.Contains(Address.ToString(), entry.Message);
        Assert.Empty(connection.ClientLog.Entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerThatFailsForAnotherReasonIsLoggedAsAnErrorAndKeepsTheConnection(bool loggedIn)
    {
        var connection = new Connection(new Failing.ClientHelloEvent(), loggedIn ? new Habbo { Id = 1, Username = "alice" } : null);

        await connection.ReceiveEmptyPacket();
        await connection.ClientLog.FirstEntry.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(connection.Client.Closed.IsCancellationRequested);
        var entry = Assert.Single(connection.ClientLog.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Empty(connection.PacketLog.Entries);
    }

    private static class Failing
    {
        [NoAuthenticationRequired]
        public sealed class ClientHelloEvent : IPacketEvent
        {
            public Task Parse(GameClient session, IIncomingPacket packet) => throw new InvalidOperationException("Server bug.");
        }
    }

    private sealed class Connection
    {
        private readonly RoutingServer _server;

        public Connection(IPacketEvent handler, Habbo? habbo)
        {
            _server = new RoutingServer(new PacketManager(new[] { handler }, PacketLog));
            Client = new FlashGameClient(_server, new FlashPacketFactory(), ClientLog)
            {
                Revision = new Revision
                {
                    IncomingIdToInternalIdMapping = new Dictionary<uint, uint> { [ClientPacketHeader.ClientHelloEvent] = ClientPacketHeader.ClientHelloEvent }
                },
                RemoteAddress = new ClientAddress(Address, true),
                DisconnectRequested = () => { }
            };

            if (habbo != null) {
                Client.SetHabbo(habbo);
            }
        }

        public FlashGameClient Client { get; }
        public RecordingLogger<PacketManager> PacketLog { get; } = new();
        public RecordingLogger<GameClient> ClientLog { get; } = new();

        public async Task ReceiveEmptyPacket()
        {
            var frame = new byte[] { 0, 0, 0, 2, 0, 0 };
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)ClientPacketHeader.ClientHelloEvent);

            Client.OnReceived(frame, 0, frame.Length);
            await _server.Handled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class RoutingServer(PacketManager manager) : IGameServer
    {
        public TaskCompletionSource Handled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Start() => true;
        public bool Stop() => true;

        public async Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet)
        {
            try {
                await manager.TryExecutePacket(client, messageId, packet);
            }
            finally {
                Handled.TrySetResult();
            }
        }

        public bool ModifyOutgoingPacket(GameClient client, IOutgoingPacket packet) => true;
        public bool HasOutgoingPacketInjectors(uint messageId) => false;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly TaskCompletionSource _firstEntry = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = new();

        public Task FirstEntry => _firstEntry.Task;

        public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
        {
            get {
                lock (_entries) {
                    return _entries.ToList();
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) {
                return;
            }

            lock (_entries) {
                _entries.Add((logLevel, formatter(state, exception), exception));
            }

            _firstEntry.TrySetResult();
        }
    }
}
