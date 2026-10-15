using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Chests;

namespace Plus.HabboHotel.Items.Interactor;

public sealed class InteractorWiredContract : IFurniInteractor
{
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null && item.GetRoom() is { } room) {
            WiredNativeContractEditor.Open(room, session, item.Id);
        }
    }
}
