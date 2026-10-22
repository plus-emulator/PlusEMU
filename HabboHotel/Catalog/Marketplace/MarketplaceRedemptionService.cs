using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Catalog.Marketplace;

public interface IMarketplaceRedemptionService
{
    void Redeem(GameClient session);
}

public sealed class MarketplaceRedemptionService(IMarketplaceOfferStore store) : IMarketplaceRedemptionService
{
    public void Redeem(GameClient session)
    {
        var habbo = session.GetHabbo();

        lock (habbo.WalletSync) {
            // A wallet saved for shutdown cannot receive credits; the sold offers stay queued for the next login.
            if (habbo.WalletClosed) {
                return;
            }

            // The store commits the claim only if the wallet can hold the total, so no sale is removed without being paid.
            if (store.ClaimSold(habbo.Id, owed => CreditBalance.AddToBalance(habbo.Credits, owed)) is not { } credits) {
                return;
            }

            habbo.Credits = credits;
            session.Send(new CreditBalanceComposer(habbo.Credits));
        }
    }
}
