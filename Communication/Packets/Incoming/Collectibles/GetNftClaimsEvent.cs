using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>The Reward Claims tab asks for the monthly rewards of a wallet: wallet.</summary>
internal sealed class GetNftClaimsEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.SendRewardClaims(session, packet.ReadString());
}
