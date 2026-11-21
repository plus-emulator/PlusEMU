using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NetCoreServer;
using Xunit;
using TcpClient = System.Net.Sockets.TcpClient;

namespace Plus.Tests;

/// <summary>A raw client socket for tests that drive a real game server on a loopback port.</summary>
internal sealed class GameSocket : IDisposable
{
    private readonly byte[] _buffer = new byte[64 * 1024];
    private Task<int>? _pendingRead;

    private GameSocket(TcpClient tcp) => Tcp = tcp;

    public TcpClient Tcp { get; }

    public NetworkStream Stream => Tcp.GetStream();

    public static async Task<GameSocket> Connect(TcpServer server, int? receiveBufferSize = null)
    {
        var tcp = new TcpClient { NoDelay = true };

        if (receiveBufferSize is { } size) {
            tcp.ReceiveBufferSize = size;
        }

        await tcp.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)server.Endpoint).Port);

        return new(tcp);
    }

    public async Task Upgrade()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var request = "GET / HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n" +
                      $"Sec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n";
        await Stream.WriteAsync(Encoding.ASCII.GetBytes(request));

        var response = new StringBuilder();
        var one = new byte[1];

        while (!response.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal)) {
            Assert.Equal(1, await Stream.ReadAsync(one).AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
            response.Append((char)one[0]);
        }

        Assert.StartsWith("HTTP/1.1 101", response.ToString());
    }

    /// <summary>True when the server sent nothing and did not close the connection within a short wait.</summary>
    public async Task<bool> StaysOpen()
    {
        var read = PendingRead();

        if (await Task.WhenAny(read, Task.Delay(TimeSpan.FromMilliseconds(300))) != read) {
            return true;
        }

        _pendingRead = null;

        return false;
    }

    /// <summary>True when the server closes the connection; data it sends first is skipped.</summary>
    public async Task<bool> IsClosed()
    {
        try {
            while (true) {
                var read = await PendingRead().WaitAsync(TimeSpan.FromSeconds(10));
                _pendingRead = null;

                if (read == 0) {
                    return true;
                }
            }
        }
        catch (IOException) {
            return true;
        }
        catch (TimeoutException) {
            return false;
        }
    }

    public void Dispose() => Tcp.Dispose();

    private Task<int> PendingRead() => _pendingRead ??= Stream.ReadAsync(_buffer).AsTask();
}
