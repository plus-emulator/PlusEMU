using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Marketplace;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Catalog.Marketplace;
using Plus.HabboHotel.Talents;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Badges;
using Xunit;

namespace Plus.Tests;

public sealed class MarketplaceAccessTests
{
    [Fact]
    public void TradingPassRequiresCompletedCitizenshipTasksAndCannotComeFromAnotherTrack()
    {
        var habbo = new Habbo();
        var definitions = new Dictionary<string, Achievement>
        {
            ["ACH_A"] = new() { Id = 42, GroupName = "ACH_A", Category = "identity", Levels = new() { [1] = new(1, 0, 0, 5) } }
        };
        var levels = new List<TalentTrackLevel> { new("citizenship", 0, "TRADE", "", [new(1, "ACH_A1", 5)]) };
        var talents = CatalogSnapshotTestSupport.Proxy<ITalentTrackManager>((_, _) => levels);
        var achievements = CatalogSnapshotTestSupport.Proxy<IAchievementManager>((_, _) => definitions);
        var policy = new MarketplaceAccessPolicy(talents, achievements);
        Assert.False(policy.HasTradingPass(habbo));
        habbo.Achievements.TryAdd("ACH_A", new("ACH_A", 1, 5));
        Assert.True(policy.HasTradingPass(habbo));
        levels[0].Type = "helper";
        Assert.False(policy.HasTradingPass(habbo));

        habbo.Inventory = new InventoryComponent { Badges = new BadgesInventoryComponent(new()) };
        habbo.Inventory.Badges.AddBadge(new Badge("ACH_TraderPass1", 0));
        Assert.True(policy.HasTradingPass(habbo));
    }

    [Fact]
    public async Task MissingTradingPassUsesTheNativeDenial()
    {
        var locks = CatalogSnapshotTestSupport.Proxy<ITradingLockService>((_, _) => false);
        var (client, sent) = HabbiconTestSupport.Client(new Habbo());
        await new GetMarketplaceCanMakeOfferEvent(locks, new MarketplaceTestAccess(false)).Parse(client, null!);
        Assert.Equal(new byte[] { 0, 0, 0, 3, 0, 0, 0, 0 }, Assert.Single(sent).Payload);
    }
}
