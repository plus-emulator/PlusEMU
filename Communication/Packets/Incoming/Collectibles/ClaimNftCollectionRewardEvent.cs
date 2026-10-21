using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>A set's reward item is claimed (AIR CollectionView.as:196): collection id, wallet.</summary>
internal sealed class ClaimNftCollectionRewardEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.ClaimCollectionReward(session, packet.ReadString(), packet.ReadString());
}
