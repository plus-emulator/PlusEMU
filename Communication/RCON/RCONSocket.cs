using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Plus.Communication.RCON.Commands;

namespace Plus.Communication.RCON;

public class RconSocket : IRconSocket
{
    internal const int MinimumSecretLength = 32;
    private IReadOnlyList<string> _allowedConnections = [];
    private byte[]? _secret;
    private readonly ICommandManager _commands;
    private readonly ILogger<RconSocket> _logger;
    private readonly ILogger<RconConnection> _connectionLogger;

    public RconSocket(ICommandManager commandManager, ILogger<RconSocket> logger, ILogger<RconConnection> connectionLogger)
    {
        _commands = commandManager;
        _logger = logger;
        _connectionLogger = connectionLogger;
    }

    public void Init(RconConfiguration configuration)
    {
        _secret = ReadSecret(configuration, _logger);
        // Swap in a complete list so the accept callback never sees a partly filled one.
        _allowedConnections = configuration.AllowedAddresses.ToList();

        try {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Parse(configuration.Hostname), configuration.Port));
            listener.Listen(0);
            listener.BeginAccept(OnCallBack, listener);
        }
        catch (Exception e) {
            throw new ArgumentException($"Could not set up Rcon socket:\n{e}");
        }
    }

    private void OnCallBack(IAsyncResult iAr)
    {
        if (iAr.AsyncState is not Socket listener) {
            return;
        }

        try {
            var socket = listener.EndAccept(iAr);
            var ip = (socket.RemoteEndPoint as IPEndPoint)?.Address.ToString();

            if (ip != null && _allowedConnections.Contains(ip)) {
                new RconConnection(socket, _connectionLogger, _secret);
            }
            else {
                socket.Close();
            }
        }
        catch (Exception) {
            // ignored
        }

        listener.BeginAccept(OnCallBack, listener);
    }

    public ICommandManager GetCommands() => _commands;

    internal static byte[]? ReadSecret(RconConfiguration configuration, ILogger logger)
    {
        if (string.IsNullOrEmpty(configuration.Secret)) {
            if (!IPAddress.TryParse(configuration.Hostname, out var address) || !IPAddress.IsLoopback(address)) {
                logger.LogWarning("RCON listens on {Hostname} without Rcon:Secret, so any process that reaches the port from an allowed address can run commands. " +
                                  "Set Rcon:Secret to a random value of at least {Length} characters.", configuration.Hostname, MinimumSecretLength);
            }

            return null;
        }

        if (configuration.Secret.Length < MinimumSecretLength) {
            throw new InvalidOperationException($"Rcon:Secret must be at least {MinimumSecretLength} characters long. Leave it empty to turn the secret off.");
        }

        return Encoding.UTF8.GetBytes(configuration.Secret);
    }
}
