using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// Room-wide configuration switches (Invisible Furni Controller, :chooser disabler): a rights holder or a wired toggle flips the
/// state between "0" and "1", and every change re-announces the room's ConfigurationItemStates.
/// </summary>
public sealed class InteractorConfigurationSwitch : IFurniInteractor
{
    public void OnPlace(GameClient? session, Item item) => Announce(item);
    public void OnRemove(GameClient? session, Item item)
    {
        if (item.GetRoom() is not { } room) {
            return;
        }

        // Observed on AIR WIN63-202610201032-107964540: a :chooser disabler taken up while on sends the flag still on, so the client
        // keeps it until the next room entry; taken up while off it sends nothing.
        if (item.Definition.InteractionType == InteractionType.ChooserDisabler) {
            if (item.LegacyDataString == "1") {
                ConfigurationItemStates.Announce(room);
            }

            return;
        }

        // The switch is still listed while it is being taken out; leave it out of the answer.
        ConfigurationItemStates.Announce(room, item);
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
            ConfigurationItemStates.Announce(room);
        }
    }
}
