using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.Users;

namespace Plus.Tests;

internal sealed class MarketplaceTestAccess(bool allowed = true) : IMarketplaceAccessPolicy
{
    public static IMarketplaceAccessPolicy Allowed { get; } = new MarketplaceTestAccess();
    public bool HasTradingPass(Habbo habbo) => allowed;
}
