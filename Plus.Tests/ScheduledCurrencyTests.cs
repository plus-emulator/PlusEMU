using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Process;
using Xunit;

namespace Plus.Tests;

public sealed class ScheduledCurrencyTests
{
    [Fact]
    public void PersistencePrecedesLiveBalancesAndPackets()
    {
        var (habbo, sent) = Player();
        (int Credits, int Duckets, int Packets)? before = null;
        var store = new Store((_, _, _) =>
        {
            before = (habbo.Credits, habbo.Duckets, sent.Count);

            return true;
        });
        using var process = Process(habbo, store);
        process.Run(null);

        Assert.Equal((100, 20, 0), before);
        Assert.Equal((7, 105, 30), Assert.Single(store.Writes));
        Assert.Equal((105, 30, 3), (habbo.Credits, habbo.Duckets, habbo.CreditsUpdateTick));
        Assert.Equal([ServerPacketHeader.CreditBalanceComposer, ServerPacketHeader.HabboActivityPointNotificationComposer],
            sent.Select(packet => packet.Header));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedPersistenceLeavesBalancesAndPacketsUnchangedAndCanRetry(bool throws)
    {
        var (habbo, sent) = Player();
        var fail = true;
        var store = new Store((_, _, _) => fail
            ? throws ? throw new InvalidOperationException("write failed") : false
            : true);
        using var process = Process(habbo, store);
        process.Run(null);
        Assert.Equal((100, 20), (habbo.Credits, habbo.Duckets));
        Assert.Empty(sent);
        fail = false;
        process.Run(null);
        Assert.Equal([(7, 105, 30), (7, 105, 30)], store.Writes);
        Assert.Equal((105, 30), (habbo.Credits, habbo.Duckets));
        Assert.Equal(2, sent.Count);
    }

    [Fact]
    public void UnflushedLiveRewardsAreKeptWhenTheTimerPays()
    {
        var (habbo, _) = Player();
        habbo.Credits = 140;
        habbo.Duckets = 25;
        var store = new Store((_, _, _) => true);
        using var process = Process(habbo, store);
        process.Run(null);
        Assert.Equal((7, 145, 35), Assert.Single(store.Writes));
        Assert.Equal((145, 35), (habbo.Credits, habbo.Duckets));
    }

    [Theory]
    [InlineData(int.MaxValue, 20, 1, 0)]
    [InlineData(100, int.MaxValue, 0, 1)]
    [InlineData(100, 20, -1, 1)]
    [InlineData(100, 20, 1, -1)]
    public void InvalidOrOverflowingRewardDoesNotPartiallyPay(int credits, int duckets, int creditReward, int ducketReward)
    {
        var (habbo, sent) = Player();
        habbo.Credits = credits;
        habbo.Duckets = duckets;
        var store = new Store((_, _, _) => true);
        using var process = Process(habbo, store, Settings(creditReward, ducketReward));
        process.Run(null);
        Assert.Empty(store.Writes);
        Assert.Empty(sent);
        Assert.Equal((credits, duckets), (habbo.Credits, habbo.Duckets));
    }

    [Fact]
    public void TheConfiguredIntervalMustElapseBeforeAnotherPayment()
    {
        var (habbo, _) = Player();
        var store = new Store((_, _, _) => true);
        using var process = Process(habbo, store);
        process.Run(null);
        process.Run(null);
        process.Run(null);
        Assert.Single(store.Writes);
        process.Run(null);
        Assert.Equal([(7, 105, 30), (7, 110, 40)], store.Writes);
    }

    [Fact]
    public void ClosedWalletNeverWritesOrPublishes()
    {
        var (habbo, sent) = Player();
        typeof(Habbo).GetField("_disconnected", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(habbo, true);
        var store = new Store((_, _, _) => true);
        using var process = Process(habbo, store);
        process.Run(null);
        Assert.Empty(store.Writes);
        Assert.Empty(sent);
    }

    [Fact]
    public void DisabledRewardsResetTheIntervalWithoutDatabaseOrNetworkWork()
    {
        var (habbo, sent) = Player();
        var store = new Store((_, _, _) => true);
        using var process = Process(habbo, store, Settings(0, 0));
        process.Run(null);
        Assert.Empty(store.Writes);
        Assert.Empty(sent);
        Assert.Equal(3, habbo.CreditsUpdateTick);
    }

    private static (Habbo Habbo, List<(uint Header, byte[] Payload)> Sent) Player()
    {
        var habbo = new Habbo { Id = 7, Credits = 100, Duckets = 20, Access = UserAccess.Empty };
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;

        return (habbo, sent);
    }

    private static ISettingsManager Settings(int credits = 5, int duckets = 10) => new TestRoomSettings(new()
    {
        ["user.currency_scheduler.credit_reward"] = credits.ToString(),
        ["user.currency_scheduler.ducket_reward"] = duckets.ToString(),
        ["user.currency_scheduler.tick"] = "3"
    });

    private static ProcessComponent Process(Habbo habbo, IUserProcessStore store, ISettingsManager? settings = null)
    {
        var process = new ProcessComponent(TestLogging.For<ProcessComponent>(), TimeProvider.System, store,
            CatalogSnapshotTestSupport.Proxy<IAchievementManager>((_, _) => false), settings ?? Settings());
        Assert.True(process.Init(habbo));

        return process;
    }

    private sealed class Store(Func<int, int, int, bool> save) : IUserProcessStore
    {
        public List<(int UserId, int Credits, int Duckets)> Writes { get; } = [];
        public void ResetDailyRespects(int userId, int respects, int petRespects, string day) => throw new NotSupportedException();
        public bool SaveCurrency(int userId, int credits, int duckets)
        {
            Writes.Add((userId, credits, duckets));

            return save(userId, credits, duckets);
        }
    }
}
