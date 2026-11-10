using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Recycler;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed class RecyclerAchievementServiceTests
{
    [Fact]
    public void RecoveryUsesAbsoluteTotalsForAllFiveAchievementsAndKeepsExistingProgress()
    {
        var habbo = new Habbo
        {
            Id = 7,
            Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) },
            HabboStats = new HabboStats(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0)
        };
        habbo.Achievements["ACH_RecycledItems"] = new("ACH_RecycledItems", 0, 5);
        var (client, _) = HabbiconTestSupport.Client(habbo);
        var calls = new List<(string Group, int Total, bool Absolute)>();
        var manager = CatalogSnapshotTestSupport.Proxy<IAchievementManager>((method, args) =>
        {
            Assert.Equal("ProgressAchievement", method);
            calls.Add(((string)args[1]!, (int)args[2]!, (bool)args[3]!));

            return false;
        });
        var store = new ProgressStore(new(7, 2, 1, 2, 1));
        var service = new RecyclerAchievementService(store, manager, TestLogging.For<RecyclerAchievementService>());
        service.Sync(client);
        service.Sync(client);
        Assert.Equal(10, calls.Count);
        Assert.Equal(calls.Take(5), calls.Skip(5));
        Assert.All(calls, call => Assert.True(call.Absolute));
        Assert.Equal(new[] { "ACH_RecycledItems", "ACH_RecycleBoxOpener", "ACH_RecycleEcoprizes", "ACH_RecyclerRarePrize", "ACH_RecyclerGrandPrize" }, calls.Take(5).Select(call => call.Group));
        Assert.Equal(new RecyclerAchievementProgress(5, 0, 0, 0, 0), store.Initial);
        Assert.Equal(5, habbo.GetAchievementData("ACH_RecycledItems")!.Progress);
    }

    private sealed class ProgressStore(RecyclerAchievementProgress totals) : IRecyclerAchievementStore
    {
        public RecyclerAchievementProgress? Initial { get; private set; }
        public RecyclerAchievementProgress? Read(int userId, RecyclerAchievementProgress initial)
        {
            Assert.Equal(7, userId);
            Initial = initial;

            return totals;
        }
    }
}
