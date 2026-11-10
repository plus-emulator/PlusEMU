using Plus.HabboHotel.Recycler;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public sealed class RecyclerModernPrizeTests
{
    [Fact]
    public void SixLevelsHaveExactAbsoluteWeights()
    {
        var config = new RecyclerConfiguration(true, 8, 10,
            [new(6, 5000, [new(1, 0, 10001)]), new(5, 400, [new(2, 2)]), new(4, 40, [new(3, 3)]),
             new(3, 10, [new(4, 4)]), new(2, 4, [new(5, 5)]), new(1, 1, [new(6, 6)])], 10);
        var distribution = RecyclerPrizeDistribution.Build(config)!.Value;
        Assert.Equal(10000, distribution.Total);
        Assert.Equal(new[] { 2, 25, 250, 1000, 2500, 6223 }, distribution.Levels.Select(level => level.Weight));
        var counts = new Dictionary<int, int>();

        for (var draw = 0; draw < 10000; draw++) {
            var service = new RecyclerService(null!, null!, TimeProvider.System, new RecyclerTestRandom([draw, 0]), TestLogging.For<RecyclerService>());
            var prize = service.Pick(config);
            counts[prize.Id] = counts.GetValueOrDefault(prize.Id) + 1;
        }

        Assert.Equal(new[] { 2, 25, 250, 1000, 2500, 6223 }, counts.OrderBy(pair => pair.Key).Select(pair => pair.Value));
    }

    [Fact]
    public void OvercommittedAndOverflowingDistributionsAreRejected()
    {
        Assert.Null(RecyclerPrizeDistribution.Build(new(true, 8, 10, [new(1, 1, []), new(2, 2, []), new(3, 2, [])])));
        Assert.Null(RecyclerPrizeDistribution.Build(new(true, 8, 10, [new(1, 1, []), new(2, int.MaxValue, []), new(3, int.MaxValue - 1, [])])));
    }

    [Fact]
    public void PurchasedStyleIsPrivateToItsOwnerAndDisabledStylesRemainUnavailable()
    {
        var owner = new Habbo { Id = 7 };
        var other = new Habbo { Id = 8 };
        var style = new ChatStyle(10001, "Paint", "", requiresOwnership: true);
        Assert.False(style.CanUse(owner));
        owner.PurchasedChatStyles.TryAdd(10001, 0);
        Assert.True(style.CanUse(owner));
        Assert.False(style.CanUse(other));
        style.Enabled = false;
        Assert.False(style.CanUse(owner));
    }

    [Fact]
    public void PurchasedStylePacketsKeepOwnershipAndStyleIdSeparate()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new Plus.Communication.Packets.Outgoing.Rooms.Session.UserPurchasableChatStylesComposer([10002, 10001, 10002]).Compose(packet);
        Assert.Equal(new object[] { 2, 10001, 10002 }, packet.Writes);
        packet = new();
        new Plus.Communication.Packets.Outgoing.Rooms.Session.UserPurchasableChatStyleChangedComposer(true, 10001).Compose(packet);
        Assert.Equal(new object[] { true, 10001 }, packet.Writes);
    }
}
