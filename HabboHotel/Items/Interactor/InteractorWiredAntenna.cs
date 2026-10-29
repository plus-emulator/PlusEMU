using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>Wired antenna: a use switches its state like any switch and, for a user with rights, sends the antenna's signal.</summary>
public sealed class InteractorWiredAntenna(IFurniInteractor toggle) : IFurniInteractor
{
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        toggle.OnTrigger(session, item, request, hasRights);

        if (session?.GetHabbo() is not { } habbo || !hasRights || item.GetRoom() is not { } room
            || room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) is not { } sender) {
            return;
        }

        room.GetWired().SendManualSignal(sender, item);
    }

    public void OnWiredTrigger(Item item) => toggle.OnWiredTrigger(item);
}
