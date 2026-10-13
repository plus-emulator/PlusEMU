using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

public sealed class InteractorInvisibleFurniControl : IFurniInteractor
{
    public void OnPlace(GameClient? session, Item item) => Announce(item);
    public void OnRemove(GameClient? session, Item item)
    {
        // The controller is still listed while it is being taken out; leave it out of the answer.
        if (item.GetRoom() is { } room) {
            InvisibleFurniState.Announce(room, item);
        }
    }
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null && hasRights) {
            Toggle(item);
        }
    }
    public void OnWiredTrigger(Item item) => Toggle(item);
    private static void Toggle(Item item)
    {
        item.LegacyDataString = item.LegacyDataString == "1" ? "0" : "1";
        item.UpdateState();
        Announce(item);
    }
    private static void Announce(Item item)
    {
        if (item.GetRoom() is { } room) {
            InvisibleFurniState.Announce(room);
        }
    }
}
