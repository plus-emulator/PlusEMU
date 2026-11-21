using System.Net;
using NetCoreServer;
using Plus.Communication.Abstractions;

namespace Plus.HabboHotel.GameClients;

public class TcpSessionProxy : TcpSession
{
    private readonly GameClient _client;
    private readonly TimeProvider _clock;
    private readonly ConnectionAdmission? _admission;
    private IDisposable? _connectionSlot;
    private bool _refused;
    private long _bytesSending;

    public TcpSessionProxy(TcpServer server, GameClient client, TimeProvider? clock = null, ConnectionAdmission? admission = null) : base(server)
    {
        _client = client;
        _clock = clock ?? TimeProvider.System;
        _admission = admission;
        OptionReceiveBufferLimit = GameClient.ReceiveBufferLimit;
        _client.Id = Id;
        _client.SendCallback = args =>
        {
            if (!Socket.Connected) {
                return false;
            }

            // Sends go straight to the socket, so a client that stops reading would keep every pending send in memory.
            var size = args.Count;

            if (Interlocked.Add(ref _bytesSending, size) > GameClient.SendBufferLimit) {
                Interlocked.Add(ref _bytesSending, -size);
                Disconnect();

                return false;
            }

            args.Completed += (_, _) => Interlocked.Add(ref _bytesSending, -size);

            try {
                if (Socket.SendAsync(args)) {
                    return true;
                }
            }
            catch (Exception) // TODO 80O: Maybe handle some potential errors.
            {
            }

            Interlocked.Add(ref _bytesSending, -size);

            return false;
        };
        _client.DisconnectRequested = () => Disconnect();
    }

    /// <summary>Runs before the first receive, so every packet sees the address. Raw TCP has no proxy headers.</summary>
    protected override void OnConnecting()
    {
        if (_admission != null) {
            var address = _admission.Resolve(((IPEndPoint)Socket.RemoteEndPoint!).Address, []);
            _client.RemoteAddress = address;
            _connectionSlot = _admission.TryAdmit(address);
            _refused = _connectionSlot == null;
        }

        _client.StartLoginDeadline(_clock);
    }

    // The session can only close once it is connected, so the refusal waits until here.
    protected override void OnConnected()
    {
        if (_refused) {
            Disconnect();
        }
    }

    protected override void OnDisconnected()
    {
        _connectionSlot?.Dispose();
        _client.OnTransportDisconnected();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        if (!_refused) {
            _client.OnReceived(buffer, offset, size);
        }
    }
}
