using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Inventory.Trading;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

[Collection("Trade game fixture")]
public sealed class TradeStartTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingShutdownRefusesToOpenATradeEvenWithTheTradeOverride(bool tradeOverride)
    {
        using var f = new TradeConfirmationServiceTests.TradeFixture(plannedShutdown: TestPlannedShutdown.Pending);
        f.Room.TradeSettings = 2;
        var alice = f.Join(1, 7);
        var bob = f.Join(2, 8);

        if (tradeOverride) {
            alice.Habbo.Access = UserAccess.Create([], [new(PermissionKeys.RoomTradeOverride, false)]);
        }

        await new InitTradeEvent(f.Trades).Parse(alice.Session, HabbiconTestSupport.Incoming(8));

        var refusal = Assert.Single(alice.Packets);
        Assert.Equal(ServerPacketHeader.TradingErrorComposer, refusal.Header);
        var payload = new FlashIncomingPacket { Buffer = refusal.Payload };
        Assert.Equal((1, "user2"), (payload.ReadInt(), payload.ReadString()));
        Assert.False(payload.HasDataRemaining());
        Assert.False(alice.RoomUser.IsTrading);
        Assert.False(bob.RoomUser.IsTrading);
        Assert.Empty(bob.Packets);
    }

    [Fact]
    public async Task WithoutAPendingShutdownATradeOpens()
    {
        using var f = new TradeConfirmationServiceTests.TradeFixture(plannedShutdown: TestPlannedShutdown.None);
        f.Room.TradeSettings = 2;
        var alice = f.Join(1, 7);
        var bob = f.Join(2, 8);

        await new InitTradeEvent(f.Trades).Parse(alice.Session, HabbiconTestSupport.Incoming(8));

        Assert.True(alice.RoomUser.IsTrading);
        Assert.True(bob.RoomUser.IsTrading);
        Assert.Equal(new[] { ServerPacketHeader.TradingStartComposer }, alice.Sent);
        Assert.Equal(new[] { ServerPacketHeader.TradingStartComposer }, bob.Sent);
    }
}
