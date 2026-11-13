using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Talents;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Catalog.Marketplace;

public interface IMarketplaceAccessPolicy
{
    bool HasTradingPass(Habbo habbo);
}

public sealed class MarketplaceAccessPolicy(ITalentTrackManager talents, IAchievementManager achievements) : IMarketplaceAccessPolicy
{
    public bool HasTradingPass(Habbo habbo) => habbo.Inventory?.Badges?.HasBadge("ACH_TraderPass1") == true || TalentTrackSnapshot.Capture(
        talents.GetLevels().Where(level => level.Type == "citizenship"), habbo, achievements.Achievements)
        .Any(level => level.State == 2 && level.Actions.Contains("TRADE"));
}
