using NetCoreServer;
using Plus.Communication.Abstractions;

namespace Plus.HabboHotel.GameClients;

public class TcpSessionProxy : TcpSession
{
    private readonly GameClient _client;
    private readonly TimeProvider _clock;
    private readonly ConnectionAdmission? _admission;

    public TcpSessionProxy(TcpServer server, GameClient client, TimeProvider? clock = null, ConnectionAdmission? admission = null) : base(server)
    {
        _client = client;
        _clock = clock ?? TimeProvider.System;
        _admission = admission;
        _client.Id = Id;
        _client.SendCallback = args =>
        {
            if (!Socket.Connected) {
                return false;
            }

            try {
                return Socket.SendAsync(args);
            }
            catch (Exception) // TODO 80O: Maybe handle some potential errors.
            {
            }

            return false;
        };
        _client.DisconnectRequested = () => Disconnect();
    }

    protected override void OnConnecting() => _client.StartLoginDeadline(_clock);

    protected override void OnDisconnected() => _client.OnTransportDisconnected();

    protected override void OnReceived(byte[] buffer, long offset, long size) => _client.OnReceived(buffer, offset, size);
}
