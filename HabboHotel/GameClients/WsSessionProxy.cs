using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetCoreServer;

namespace Plus.HabboHotel.GameClients;

public class WsSessionProxy : WsSession
{
    internal const byte PongOpcode = WebSocket.WS_FIN | WebSocket.WS_PONG;
    internal const int MaxControlPayload = 125;

    private static readonly FieldInfo? WebSocketField = typeof(WsSession).GetField("WebSocket", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? SendLockField = typeof(WebSocket).GetField("WsSendLock", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly GameClient _client;
    private readonly ILogger _logger;

    public WsSessionProxy(WsServer server, GameClient client, ILogger? logger = null) : base(server)
    {
        _client = client;
        _logger = logger ?? NullLogger.Instance;
        SendLock = ResolveSendLock(this) ?? new object();
        _client.Id = Id;
        _client.SendCallback = args =>
        {
            if (!Socket.Connected) {
                return false;
            }

            var buffer = args.MemoryBuffer.ToArray();
            SendBinaryAsync(buffer, 0, buffer.Length);

            // The WebSocket queue owns a copy; no SocketAsyncEventArgs operation is pending.
            return false;
        };
        _client.DisconnectRequested = () => Disconnect();
    }

    /// <summary>The library's lock around its shared frame send buffer; every frame this session writes takes it.</summary>
    internal object SendLock { get; }

    protected override void OnConnected()
    {
        base.OnConnected();
    }

    protected override void OnDisconnected() => _client.OnTransportDisconnected();

    /// <summary>
    /// Every WebSocket callback (frames, ping, pong, close) runs from here on a thread-pool thread, where an
    /// exception would end the process. A failing frame closes this session only.
    /// </summary>
    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        try {
            base.OnReceived(buffer, offset, size);
        }
        catch (Exception exception) {
            Fail(exception, "receive");
        }
    }

    public override void OnWsReceived(byte[] buffer, long offset, long size)
    {
        try {
            _client.OnReceived(buffer, offset, size);
        }
        catch (Exception exception) {
            Fail(exception, "frame");
        }
    }

    /// <summary>
    /// NetCoreServer 7.0.0 frames a pong as if it were a close frame with a status code, which reads past the ping
    /// payload and throws for any non-empty ping. Build the pong here instead.
    /// </summary>
    public override void OnWsPing(byte[] buffer, long offset, long size)
    {
        try {
            if (size > MaxControlPayload) {
                // RFC 6455 5.5: control frame payloads are at most 125 bytes.
                Disconnect();

                return;
            }

            var frame = BuildPongFrame(buffer.AsSpan((int)offset, (int)size));

            lock (SendLock) {
                SendAsync(frame);
            }
        }
        catch (Exception exception) {
            Fail(exception, "ping");
        }
    }

    public override void OnWsClose(byte[] buffer, long offset, long size, int status = 1000)
    {
        try {
            base.OnWsClose(buffer, offset, size, status);
        }
        catch (Exception exception) {
            Fail(exception, "close");
        }
    }

    internal static byte[] BuildPongFrame(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxControlPayload) {
            throw new ArgumentOutOfRangeException(nameof(payload), payload.Length, "A control frame payload is at most 125 bytes.");
        }

        var frame = new byte[2 + payload.Length];
        frame[0] = PongOpcode;
        frame[1] = (byte)payload.Length;
        payload.CopyTo(frame.AsSpan(2));

        return frame;
    }

    internal static object? ResolveSendLock(WsSession session) =>
        WebSocketField?.GetValue(session) is { } webSocket ? SendLockField?.GetValue(webSocket) : null;

    private void Fail(Exception exception, string stage)
    {
        _logger.LogError(exception, "WebSocket {Stage} failed for session {SessionId}; closing it", stage, Id);

        try {
            Disconnect();
        }
        catch (Exception disconnectException) {
            _logger.LogError(disconnectException, "Failed to close WebSocket session {SessionId}", Id);
        }
    }
}
