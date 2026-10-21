using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

internal sealed class InteractorEffectBox(IItemRedemptionService redemptions, TimeProvider time) : IFurniInteractor
{
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null) {
            redemptions.OpenEffectBox(session, item, time.GetUtcNow());
        }
    }
}
