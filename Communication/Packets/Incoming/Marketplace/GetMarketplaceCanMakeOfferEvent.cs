using Plus.Communication.Packets.Outgoing.Marketplace;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Catalog.Marketplace;

namespace Plus.Communication.Packets.Incoming.Marketplace;

internal class GetMarketplaceCanMakeOfferEvent(ITradingLockService tradingLocks, IMarketplaceAccessPolicy access) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var habbo = session.GetHabbo();
        var errorCode = tradingLocks.IsLocked(habbo) ? MarketplaceOfferEligibility.TradingLocked
            : access.HasTradingPass(habbo) ? MarketplaceOfferEligibility.Allowed : MarketplaceOfferEligibility.NoTradingPass;
        session.Send(new MarketplaceCanMakeOfferResultComposer(errorCode));

        return Task.CompletedTask;
    }
}
