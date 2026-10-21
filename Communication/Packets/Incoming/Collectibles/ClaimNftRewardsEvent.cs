using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Incoming.Collectibles;

/// <summary>Claim all on the Reward Claims tab (AIR RewardClaimsTab.as:149). AIR always sends two empty strings, so they are not read.</summary>
internal sealed class ClaimNftRewardsEvent(ICollectiblesHubService hub) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => hub.ClaimRewards(session);
}
