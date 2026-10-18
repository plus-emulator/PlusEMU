using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Chests;

namespace Plus.HabboHotel.Items.Interactor;

public sealed class InteractorWiredChest : IFurniInteractor
{
    public void OnPlace(GameClient? session, Item item)
    {
        var room = item.GetRoom();

        // A chest is locked and published when it is placed; moving it changes neither (the move sends its one ObjectUpdate).
        if (room == null || item.Moving) {
            return;
        }

        room.GetWired().WithChests(module =>
        {
            module.Read(item);
            module.Store.SaveSettings(item, (int)item.OwnerId, false, false, old => old with { Locked = true });
            module.Refresh(item);
        });
    }
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null && item.GetRoom() is { } room) {
            room.GetWired().WithChests(module => module.RequestOpen(session, item));
        }
    }
}
