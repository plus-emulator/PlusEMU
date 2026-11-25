using NetCoreServer;
using Plus.Communication.Abstractions;
using Microsoft.Extensions.Logging;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Flash;

public class FlashClientFactory : IGameClientFactory<TcpSessionProxy, TcpServer>
{
    private readonly FlashPacketFactory _packetFactory;
    private readonly IRevisionsCache _revisionsCache;
    private readonly ILogger<GameClient> _logger;
    private readonly TimeProvider _clock;

    public FlashClientFactory(FlashPacketFactory packetFactory, IRevisionsCache revisionsCache, ILogger<GameClient> logger, TimeProvider clock)
    {
        _packetFactory = packetFactory;
        _revisionsCache = revisionsCache;
        _logger = logger;
        _clock = clock;
    }

    public TcpSessionProxy Create(TcpServer server) =>
        new((FlashServer)server, new FlashGameClient((FlashServer)server, _packetFactory, _logger) { Revision = _revisionsCache.InternalRevision }, _clock,
            ((FlashServer)server).Admission);
}
