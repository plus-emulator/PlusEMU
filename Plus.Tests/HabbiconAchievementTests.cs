using Microsoft.Extensions.Logging.Abstractions;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Habbicons;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class HabbiconAchievementTests
{
    [Fact]
    public void ReconciliationCountsOwnedIconsAndCompletedCollectionsWithoutAddingTotals()
    {
        var calls = new List<(int User, string Group, int Progress, bool Absolute)>();
        var manager = CatalogSnapshotTestSupport.Proxy<IAchievementManager>((method, args) =>
        {
            Assert.Equal(nameof(IAchievementManager.ProgressAchievement), method);
            calls.Add((((GameClient)args![0]!).GetHabbo().Id, (string)args[1]!, (int)args[2]!, (bool)args[3]!));

            return true;
        });
        var service = new HabbiconAchievementService(manager);
        var (owner, _) = HabbiconTestSupport.Client(new Habbo { Id = 7 });
        var (other, _) = HabbiconTestSupport.Client(new Habbo { Id = 8 });
        var snapshot = HabbiconTestSupport.Snapshot();
        var owned = snapshot.Items.ToDictionary();
        owned[71] = owned[71] with { State = HabbiconState.Owned };
        snapshot = snapshot with { Items = owned, Collections = [snapshot.Collections[0] with { Completed = true }] };
        service.Reconcile(owner, snapshot);
        service.Reconcile(owner, snapshot);
        service.Reconcile(other, HabbiconTestSupport.Snapshot());
        service.Used(owner);

        Assert.Equal(new[] {
            (7, "ACH_HabbiconCollected", 2, true), (7, "ACH_HabbiconCollectionComp", 1, true),
            (7, "ACH_HabbiconCollected", 2, true), (7, "ACH_HabbiconCollectionComp", 1, true),
            (8, "ACH_HabbiconCollected", 1, true), (8, "ACH_HabbiconCollectionComp", 0, true),
            (7, "ACH_HabbiconUsed", 1, false)
        }, calls);
    }

    [Theory]
    [InlineData(HabbiconAction.Buy, true)]
    [InlineData(HabbiconAction.BuyCollection, true)]
    [InlineData(HabbiconAction.Claim, true)]
    [InlineData(HabbiconAction.Favorite, false)]
    [InlineData(HabbiconAction.Unfavorite, false)]
    public void OnlySuccessfulAcquisitionsReconcileAchievements(HabbiconAction action, bool reconciles)
    {
        var store = new HabbiconTestSupport.Service();
        var calls = new List<HabbiconSnapshot>();
        var achievements = CatalogSnapshotTestSupport.Proxy<IHabbiconAchievementService>((method, args) =>
        {
            Assert.Equal(nameof(IHabbiconAchievementService.Reconcile), method);
            Assert.NotEmpty(store.Actions);
            calls.Add((HabbiconSnapshot)args![1]!);

            return null;
        });
        var service = new HabbiconPresentationService(store, NullLogger<HabbiconPresentationService>.Instance, achievements);
        var (client, _) = HabbiconTestSupport.Client(new Habbo());
        service.Change(client, action, 61);
        Assert.Equal(reconciles ? 1 : 0, calls.Count);
        calls.Clear();
        store.Rejection = 4;
        service.Change(client, action, 61);
        Assert.Empty(calls);
    }
}
