using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>A set's bonus item is claimed (AIR CollectionView.as:186): collection id, wallet.</summary>
internal sealed class ClaimNftCollectionBonusEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.ClaimCollectionBonus(session, packet.ReadString(), packet.ReadString());
}
