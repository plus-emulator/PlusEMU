using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Minting tab asks for the mint token packs (AIR MintInventoryListTab.as:304).</summary>
internal sealed class GetMintTokenOffersEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendMintTokenOffers(session);
}
