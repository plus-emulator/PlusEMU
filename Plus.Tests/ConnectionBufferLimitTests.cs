using System.Buffers.Binary;
using System.Net;
using System.Text;
using NetCoreServer;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

public sealed class ConnectionBufferLimitTests
{
    private const uint BulkMessageId = 1;

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

    /// <summary>A masked binary frame header; the zero mask leaves the payload as written.</summary>
    private static byte[] FrameHeader(long payloadLength)
    {
        var header = new byte[14];
        header[0] = 0x82;
        header[1] = 0x80 | 127;
        BinaryPrimitives.WriteInt64BigEndian(header.AsSpan(2), payloadLength);

        return header;
    }

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
