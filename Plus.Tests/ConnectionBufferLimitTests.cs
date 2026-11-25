using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using NetCoreServer;
using Plus.Communication.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

public sealed class ConnectionBufferLimitTests
{
    private const uint BulkMessageId = 1;
    private const uint SlowMessageId = 2;
    private const int FillerPacketSize = 16 * 1024;
    private const int TinyChunkData = GameClient.ReceiveBufferLimit / 32;

    [Fact]
    public async Task AWebSocketMessageOverTheReceiveLimitClosesTheConnection()
    {
        using var server = new WsGameServer();
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Upgrade();

            var payloadLength = 2L * GameClient.ReceiveBufferLimit;
            await WriteUntilClosed(socket, FrameHeader(payloadLength), GameClient.ReceiveBufferLimit + 64 * 1024);

            Assert.True(await socket.IsClosed());
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task AnUpgradeRequestOverTheReceiveLimitClosesTheConnection()
    {
        using var server = new WsGameServer();
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var header = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nX-Filler: ");
            await WriteUntilClosed(socket, header, GameClient.ReceiveBufferLimit + 64 * 1024, filler: (byte)'a');

            Assert.True(await socket.IsClosed());
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task TheLargestGamePacketInOneWebSocketMessageKeepsTheConnection()
    {
        using var server = new WsGameServer();
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Upgrade();

            var packet = new byte[4 + 500000];
            BinaryPrimitives.WriteInt32BigEndian(packet, 500000);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4), 4000);
            await socket.Stream.WriteAsync(FrameHeader(packet.Length));
            await socket.Stream.WriteAsync(packet);

            Assert.True(await socket.StaysOpen());
            Assert.False(server.Client!.Closed.IsCancellationRequested);
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task AWebSocketClientThatStopsReadingIsDisconnected()
    {
        using var server = new WsGameServer();
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server, receiveBufferSize: 4096);
            await socket.Upgrade();

            Assert.True(await SendUntilClosed(server.Client!));
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task AFlashClientThatStopsReadingIsDisconnected()
    {
        using var server = new TcpGameServer();
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server, receiveBufferSize: 4096);
            var client = await server.Connected.Task.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(await SendUntilClosed(client));
        }
        finally {
            server.Stop();
        }
    }

    [Fact]
    public async Task AFlashClientThatKeepsSendingWhileAPacketIsHandledIsDisconnected()
    {
        var handler = new SlowHandler();
        using var server = new SlowTcpServer(handler);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Stream.WriteAsync(Packet(SlowMessageId, 0));
            await handler.Handling.WaitAsync(TimeSpan.FromSeconds(5));

            await SendPaced(socket, 2L * GameClient.ReceiveBufferLimit);

            Assert.True(await socket.IsClosed());
        }
        finally {
            handler.Release();
            server.Stop();
        }
    }

    [Fact]
    public async Task ManyTinyWebSocketMessagesWhileAPacketIsHandledCloseTheConnection()
    {
        var handler = new SlowHandler();
        using var server = new SlowWsServer(handler);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Upgrade();
            await socket.Stream.WriteAsync(MaskedFrame(Packet(SlowMessageId, 0)));
            await handler.Handling.WaitAsync(TimeSpan.FromSeconds(5));

            // Eight-byte frames in 8 KiB writes never split across reads; the library fails on a frame header that does.
            byte[] payload = [0, 0];
            var message = MaskedFrame(payload);
            const int messagesPerWrite = 1024;
            var batch = new byte[messagesPerWrite * message.Length];

            for (var i = 0; i < messagesPerWrite; i++) {
                message.CopyTo(batch, i * message.Length);
            }

            try {
                for (var sent = 0; sent < TinyChunkData && !handler.Client!.Closed.IsCancellationRequested; sent += messagesPerWrite * payload.Length) {
                    await socket.Stream.WriteAsync(batch);
                }
            }
            catch (IOException) {
            }

            Assert.True(await socket.IsClosed());
        }
        finally {
            handler.Release();
            server.Stop();
        }
    }

    [Fact]
    public async Task ManyOneByteFlashWritesWhileAPacketIsHandledCloseTheConnection()
    {
        var handler = new SlowHandler();
        using var server = new SlowTcpServer(handler);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            await socket.Stream.WriteAsync(Packet(SlowMessageId, 0));
            await handler.Handling.WaitAsync(TimeSpan.FromSeconds(5));

            var one = new byte[1];

            // A busy server can read several bytes as one chunk, so keep sending until the data alone passes the limit.
            try {
                for (var sent = 0; sent <= GameClient.ReceiveBufferLimit && !handler.Client!.Closed.IsCancellationRequested; sent++) {
                    socket.Tcp.Client.Send(one);
                }
            }
            catch (SocketException) {
            }

            Assert.True(await socket.IsClosed());
        }
        finally {
            handler.Release();
            server.Stop();
        }
    }

    [Fact]
    public async Task DataUnderTheReceiveLimitWaitsForASlowHandlerAndIsCountedOnlyUntilHandled()
    {
        var handler = new SlowHandler();
        using var server = new SlowTcpServer(handler);
        Assert.True(server.Start());

        try {
            using var socket = await GameSocket.Connect(server);
            var queued = 3L * GameClient.ReceiveBufferLimit / 4;

            for (var round = 1; round <= 2; round++) {
                await socket.Stream.WriteAsync(Packet(SlowMessageId, 0));
                await handler.Handling.WaitAsync(TimeSpan.FromSeconds(5));
                var sent = await SendPaced(socket, queued);

                Assert.True(await socket.StaysOpen());
                handler.Release();
                Assert.True(await handler.HandledBulk(round * (sent / FillerPacketSize)));
            }

            Assert.False(handler.Client!.Closed.IsCancellationRequested);
        }
        finally {
            handler.Release();
            server.Stop();
        }
    }

    [Fact]
    public async Task ALimitCloseLogsOneWarningPerAddressPerMinute()
    {
        var log = new WarningLog();
        var handler = new SlowHandler();
        var address = IPAddress.Parse("198.51.100.23");
        var one = new byte[1];

        try {
            for (var connection = 0; connection < 2; connection++) {
                var client = handler.NewClient(log);
                client.RemoteAddress = new ClientAddress(address, true);
                var slow = Packet(SlowMessageId, 0);
                client.OnReceived(slow, 0, slow.Length);
                await handler.Handling.WaitAsync(TimeSpan.FromSeconds(5));

                for (var chunk = 0; chunk < GameClient.ReceiveBufferLimit / 256; chunk++) {
                    client.OnReceived(one, 0, 1);
                }

                Assert.True(client.Closed.IsCancellationRequested);
            }

            var warning = Assert.Single(log.Warnings);
            Assert.Contains("198.51.100.23", warning);
            Assert.Contains("receive queue", warning);
        }
        finally {
            handler.Release();
            handler.Release();
        }
    }

    /// <summary>Sends far more than the limit plus what the kernel buffers hold; true once the server closed the client.</summary>
    private static async Task<bool> SendUntilClosed(GameClient client)
    {
        var bulk = new BulkComposer(256 * 1024);
        var total = 0L;

        while (total < GameClient.SendBufferLimit + 64L * 1024 * 1024 && !client.Closed.IsCancellationRequested) {
            client.Send(bulk);
            total += bulk.Size;
        }

        for (var i = 0; i < 50 && !client.Closed.IsCancellationRequested; i++) {
            await Task.Delay(100);
        }

        return client.Closed.IsCancellationRequested;
    }

    private static async Task WriteUntilClosed(GameSocket socket, byte[] header, int bodyLength, byte filler = 0)
    {
        var chunk = new byte[64 * 1024];
        Array.Fill(chunk, filler);

        try {
            await socket.Stream.WriteAsync(header);

            for (var sent = 0; sent < bodyLength; sent += chunk.Length) {
                await socket.Stream.WriteAsync(chunk);
            }
        }
        catch (IOException) {
        }
    }

    /// <summary>Writes whole filler packets in small paced writes, so no single read fills the transport buffer; returns the bytes sent.</summary>
    private static async Task<long> SendPaced(GameSocket socket, long total)
    {
        var filler = Packet(BulkMessageId, FillerPacketSize - 6);
        var sent = 0L;

        try {
            while (sent + filler.Length <= total) {
                await socket.Stream.WriteAsync(filler);
                sent += filler.Length;
                await Task.Delay(1);
            }
        }
        catch (IOException) {
        }

        return sent;
    }

    private static byte[] Packet(uint messageId, int bodyLength)
    {
        var packet = new byte[6 + bodyLength];
        BinaryPrimitives.WriteInt32BigEndian(packet, 2 + bodyLength);
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(4), (ushort)messageId);

        return packet;
    }

    /// <summary>A masked binary frame header; the zero mask leaves the payload as written.</summary>
    private static byte[] FrameHeader(long payloadLength)
    {
        var header = new byte[14];
        header[0] = 0x82;
        header[1] = 0x80 | 127;
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(2), payloadLength);

        return header;
    }

    private static byte[] MaskedFrame(byte[] payload) => [0x82, (byte)(0x80 | payload.Length), 0, 0, 0, 0, .. payload];

    private static FlashGameClient NewClient() => new(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient)
    {
        Revision = new Revision { InternalIdToOutgoingIdMapping = new Dictionary<uint, uint> { [BulkMessageId] = BulkMessageId } }
    };

    private sealed class BulkComposer(int size) : IServerPacket
    {
        public uint MessageId => BulkMessageId;

        public int Size => size;

        public void Compose(IOutgoingPacket packet)
        {
            for (var i = 0; i < size / 4; i++) {
                packet.WriteInt(i);
            }
        }
    }

    private sealed class WsGameServer() : WsServer(IPAddress.Loopback, 0)
    {
        public GameClient? Client { get; private set; }

        protected override WsSession CreateSession()
        {
            Client = NewClient();

            return new WsSessionProxy(this, Client);
        }
    }

    /// <summary>A packet handler whose slow message waits until the test releases it.</summary>
    private sealed class SlowHandler : IGameServer
    {
        private readonly SemaphoreSlim _released = new(0);
        private readonly SemaphoreSlim _handling = new(0);
        private int _bulkHandled;

        public GameClient? Client { get; private set; }

        public Task Handling => _handling.WaitAsync();

        public void Release() => _released.Release();

        public async Task<bool> HandledBulk(long count)
        {
            for (var i = 0; i < 100 && Volatile.Read(ref _bulkHandled) < count; i++) {
                await Task.Delay(100);
            }

            return Volatile.Read(ref _bulkHandled) == count;
        }

        public GameClient NewClient(ILogger<GameClient>? logger = null)
        {
            var mapping = new Dictionary<uint, uint> { [BulkMessageId] = BulkMessageId, [SlowMessageId] = SlowMessageId };
            Client = new FlashGameClient(this, new FlashPacketFactory(), logger ?? TestLogging.GameClient)
            {
                Revision = new Revision { IncomingIdToInternalIdMapping = mapping }
            };

            return Client;
        }

        public bool Start() => true;

        public bool Stop() => true;

        public Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet)
        {
            if (messageId != SlowMessageId) {
                Interlocked.Increment(ref _bulkHandled);

                return Task.CompletedTask;
            }

            _handling.Release();

            return _released.WaitAsync();
        }

        public bool ModifyOutgoingPacket(GameClient client, IOutgoingPacket packet) => true;

        public bool HasOutgoingPacketInjectors(uint messageId) => false;
    }

    private sealed class WarningLog : ILogger<GameClient>
    {
        public List<string> Warnings { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) {
                lock (Warnings) {
                    Warnings.Add(formatter(state, exception));
                }
            }
        }
    }

    private sealed class SlowTcpServer(SlowHandler handler) : TcpServer(IPAddress.Loopback, 0)
    {
        protected override TcpSession CreateSession() => new TcpSessionProxy(this, handler.NewClient());
    }

    private sealed class SlowWsServer(SlowHandler handler) : WsServer(IPAddress.Loopback, 0)
    {
        protected override WsSession CreateSession() => new WsSessionProxy(this, handler.NewClient());
    }

    private sealed class TcpGameServer() : TcpServer(IPAddress.Loopback, 0)
    {
        private GameClient? _client;

        public TaskCompletionSource<GameClient> Connected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override TcpSession CreateSession()
        {
            _client = NewClient();

            return new TcpSessionProxy(this, _client);
        }

        protected override void OnConnected(TcpSession session) => Connected.TrySetResult(_client!);
    }
}
