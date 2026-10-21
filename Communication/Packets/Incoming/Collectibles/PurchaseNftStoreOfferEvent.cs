using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>A shop offer is bought with emeralds (AIR HabboCatalog.purchaseNftOffer): product code, wallet.</summary>
internal sealed class PurchaseNftStoreOfferEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.PurchaseStoreOffer(session, packet.ReadString(), packet.ReadString());
}
