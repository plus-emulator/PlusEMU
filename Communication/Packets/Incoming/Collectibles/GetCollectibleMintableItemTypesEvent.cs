using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Minting tab asks which furni can be minted (AIR MintInventoryListTab.as:291).</summary>
internal sealed class GetCollectibleMintableItemTypesEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendMintableItemTypes(session);
}
