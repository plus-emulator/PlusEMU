using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>A mint token pack is bought with silver (AIR HabboCatalog.purchaseMintTokens): offer id, wallet.</summary>
internal sealed class PurchaseMintTokensEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.PurchaseMintTokens(session, packet.ReadInt(), packet.ReadString());
}
