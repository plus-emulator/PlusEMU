using Microsoft.Extensions.Options;
using NetCoreServer;
using Plus.Communication.Packets;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Flash;
using Microsoft.Extensions.Logging;

namespace Plus.Communication.Abstractions;

public abstract class TcpGameServer<TGameServerOptions> : TcpServer, IGameServer
    where TGameServerOptions : class, IGameServerOptions
{
    private readonly IGameClientFactory<TcpSessionProxy, TcpServer> _clientFactory;
    private readonly IPacketManager _packetManager;
    private readonly IReadOnlyDictionary<uint, IIncomingPacketInjector[]> _incomingInjectors;
    private readonly IReadOnlyDictionary<uint, IOutgoingPacketInjector[]> _outgoingInjectors;
    private readonly ILogger _logger;

    protected TcpGameServer(IOptions<TGameServerOptions> options,
        IGameClientFactory<TcpSessionProxy, TcpServer> clientFactory,
        IPacketManager packetManager,
        IEnumerable<IIncomingPacketInjector> incomingInjectors,
        IEnumerable<IOutgoingPacketInjector> outgoingInjectors, ILogger logger) : base(options.Value.Hostname,
        options.Value.Port)
    {
        _clientFactory = clientFactory;
        _packetManager = packetManager;
        _logger = logger;
        _incomingInjectors = incomingInjectors.GroupBy(x => x.MessageId).ToDictionary(x => x.Key, x => x.ToArray());
        _outgoingInjectors = outgoingInjectors.GroupBy(x => x.MessageId).ToDictionary(x => x.Key, x => x.ToArray());
        Admission = new(options.Value, TimeProvider.System, logger);
    }

    internal ConnectionAdmission Admission { get; }

    protected override TcpSession CreateSession() => _clientFactory.Create(this);

    public Task PacketReceived(GameClient client, uint messageId, IIncomingPacket packet)
    {
        packet.MessageId = messageId;

        if (!InvokeInjectors(_incomingInjectors, messageId, injector => injector.ModifyIncomingPacket(this, client, packet))) {
            return Task.CompletedTask;
        }

        packet.Stream.Position = 0;

        return _packetManager.TryExecutePacket(client, messageId, packet);
    }

    public bool ModifyOutgoingPacket(GameClient client, IOutgoingPacket packet) =>
        InvokeInjectors(_outgoingInjectors, (uint)packet.MessageId, injector => injector.ModifyOutgoingPacket(this, client, packet));

    public bool HasOutgoingPacketInjectors(uint messageId) => _outgoingInjectors.ContainsKey(messageId);

    private bool InvokeInjectors<T>(IReadOnlyDictionary<uint, T[]> injectors, uint messageId, Action<T> invoke)
    {
        if (!injectors.TryGetValue(messageId, out var matches)) {
            return true;
        }

        foreach (var injector in matches) {
            try {
                invoke(injector);
            }
            catch (Exception exception) {
                _logger.LogError(exception, "Packet injector {InjectorType} failed for message {MessageId}; packet aborted", injector!.GetType().Name, messageId);

                return false;
            }
        }

        return true;
    }
}

public interface IGameClientFactory<TGameClient, TServer> : IGameClientFactory
{
    TGameClient Create(TServer server);
}
