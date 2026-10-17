using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetCoreServer;
using TcpClient = System.Net.Sockets.TcpClient;
using Plus.Communication.Flash;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Xunit;

namespace Plus.Tests;

public class WebSocketControlFrameTests
{
    [Fact]
    public void LibraryPongFramingThrowsForAPingPayload()
    {
        // NetCoreServer 7.0.0 treats every control opcode as a close frame with a two-byte status and reads past the payload.
        var webSocket = new WebSocket(new WsServer(IPAddress.Loopback, 0));

        Assert.Throws<IndexOutOfRangeException>(() => webSocket.PrepareSendFrame(WsSessionProxy.PongOpcode, false, new byte[] { 1, 2, 3, 4 }));
    }

    [Fact]
    public void PongFrameEchoesThePingPayload()
    {
        var frame = WsSessionProxy.BuildPongFrame(new byte[] { 1, 2, 3 });

        Assert.Equal(new byte[] { 0x8A, 3, 1, 2, 3 }, frame);
        Assert.Equal(new byte[] { 0x8A, 0 }, WsSessionProxy.BuildPongFrame(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void SessionTakesTheLibrarySendLock()
    {
        using var server = new ProxyServer();
        using var session = new WsSessionProxy(server, NewClient());

        Assert.Same(WsSessionProxy.ResolveSendLock(session), session.SendLock);
        Assert.NotNull(WsSessionProxy.ResolveSendLock(session));
    }

    [Fact]
    public async Task PingWithPayloadGetsAPongAndKeepsTheSession()
    {
        using var server = new ProxyServer();
        Assert.True(server.Start());
        using var tcp = await Connect(server);
        var stream = tcp.GetStream();

        var payload = Encoding.ASCII.GetBytes("keepalive");
        await stream.WriteAsync(MaskedFrame(0x89, payload));
        Assert.Equal(new byte[] { 0x8A, (byte)payload.Length }.Concat(payload).ToArray(), await ReadExactly(stream, 2 + payload.Length));

        // The same session still answers.
        await stream.WriteAsync(MaskedFrame(0x89, new byte[] { 7 }));
        Assert.Equal(new byte[] { 0x8A, 1, 7 }, await ReadExactly(stream, 3));

        server.Stop();
    }

    [Fact]
    public async Task OversizedPingClosesOnlyThatSession()
    {
        using var server = new ProxyServer();
        Assert.True(server.Start());
        using var tcp = await Connect(server);
        var stream = tcp.GetStream();

        await stream.WriteAsync(MaskedFrame(0x89, new byte[200]));
        Assert.True(await IsClosed(stream));

        using var other = await Connect(server);
        await other.GetStream().WriteAsync(MaskedFrame(0x89, new byte[] { 9 }));
        Assert.Equal(new byte[] { 0x8A, 1, 9 }, await ReadExactly(other.GetStream(), 3));

        server.Stop();
    }

    [Fact]
    public async Task FailingReceiveDisconnectsInsteadOfEscaping()
    {
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ThrowingClient { DisconnectRequested = () => disconnected.TrySetResult() };

        client.OnReceived(new byte[] { 0, 0, 0, 2, 0, 1 }, 0, 6);

        await disconnected.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private static FlashGameClient NewClient() => new(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient)
    {
        Revision = new Revision()
    };

    private static async Task<TcpClient> Connect(ProxyServer server)
    {
        var tcp = new TcpClient { NoDelay = true, ReceiveTimeout = 5000 };
        await tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)server.Endpoint).Port);
        var stream = tcp.GetStream();
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var request = "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                      $"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        var response = new StringBuilder();
        var one = new byte[1];

        while (!response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            Assert.Equal(1, await stream.ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            response.Append((char)one[0]);
        }

        Assert.StartsWith("HTTP/1.1 101", response.ToString());

        return tcp;
    }

    private static byte[] MaskedFrame(byte opcode, byte[] payload)
    {
        var mask = new byte[] { 0x11, 0x22, 0x33, 0x44 };
        var header = payload.Length <= 125
            ? new[] { opcode, (byte)(0x80 | payload.Length) }
            : new[] { opcode, (byte)(0x80 | 126), (byte)(payload.Length >> 8), (byte)payload.Length };

        return header.Concat(mask).Concat(payload.Select((value, index) => (byte)(value ^ mask[index % 4]))).ToArray();
    }

    private static async Task<bool> IsClosed(NetworkStream stream)
    {
        try {
            return await stream.ReadAsync(new byte[16]).AsTask().WaitAsync(TimeSpan.FromSeconds(5)) == 0;
        }
        catch (IOException) {
            return true;
        }
    }

    private static async Task<byte[]> ReadExactly(NetworkStream stream, int count)
    {
        var buffer = new byte[count];
        await stream.ReadExactlyAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        return buffer;
    }

    private sealed class ProxyServer() : WsServer(IPAddress.Loopback, 0)
    {
        protected override WsSession CreateSession() => new WsSessionProxy(this, NewClient());
    }

    private sealed class ThrowingClient() : FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient)
    {
        internal override (bool Complete, bool Malformed, uint MessageId, int HeaderLength, int Length) GetMessageIdAndPacketLength(ReadOnlyMemory<byte> buffer) =>
            throw new InvalidOperationException("broken frame");
    }
}
