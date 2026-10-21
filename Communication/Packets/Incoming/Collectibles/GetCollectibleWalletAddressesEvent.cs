using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The hub asks for the user's wallets (AIR CollectiblesView.as:321).</summary>
internal sealed class GetCollectibleWalletAddressesEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendWallets(session);
}
