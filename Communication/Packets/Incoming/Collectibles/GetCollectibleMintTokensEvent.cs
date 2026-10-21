using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Minting tab asks how many mint tokens a wallet has (AIR MintInventoryListTab.as:182): wallet.</summary>
internal sealed class GetCollectibleMintTokensEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendMintTokens(session, packet.ReadString());
}
