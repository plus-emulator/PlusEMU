using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

// AIR furniture_effectbox logic: only the owner gets the dialog, and its OK button sends the plain use packet.
internal sealed class InteractorEffectBox(IItemRedemptionService redemptions, TimeProvider time) : IFurniInteractor
{
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null) {
            redemptions.OpenEffectBox(session, item, time.GetUtcNow());
        }
    }
}
