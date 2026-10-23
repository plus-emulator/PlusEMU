using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The hub asks for the collector score of a wallet: wallet.</summary>
internal sealed class GetCollectorScoreEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendCollectorScore(session, packet.ReadString());
}
