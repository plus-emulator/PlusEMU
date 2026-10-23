using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Links;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>WIRED Link (wf_link): using it opens the link window for its owner.</summary>
public sealed class InteractorWiredLink : IFurniInteractor
{
    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        if (session != null) {
            WiredLinkController.Open(session, item);
        }
    }
}
