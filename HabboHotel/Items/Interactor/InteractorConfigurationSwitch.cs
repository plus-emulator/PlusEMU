using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// Room-wide configuration switches (Invisible Furni Controller, :chooser disabler, hand item blocker, wired disabler): a rights holder
/// or a wired toggle flips the state between "0" and "1". The first three are room flags, so every change re-announces the flags.
/// </summary>
public sealed class InteractorConfigurationSwitch : IFurniInteractor
{
    public void OnPlace(GameClient? session, Item item)
    {
        // Observed on AIR WIN63-202610201032-107964540: placing the Invisible Furni Controller sends the flags; the other furni send nothing.
        if (item.Definition.InteractionType == InteractionType.InvisibleFurniControl) {
            Announce(item);
            Refresh(item);
        }
    }
    public void OnRemove(GameClient? session, Item item)
    {
        if (item.GetRoom() is not { } room || !ConfigurationItemStates.IsFlag(item.Definition.InteractionType)) {
            return;
        }

        // Observed on AIR WIN63-202610201032-107964540: a flag furni taken up while on sends the flag still on, so the client keeps it
        // until the next room entry; taken up while off it sends nothing.
        if (item.LegacyDataString == "1") {
            ConfigurationItemStates.Announce(room);

            // The controller is still listed while it is being taken out; the furni come back without it.
            if (item.Definition.InteractionType == InteractionType.InvisibleFurniControl) {
                ConfigurationItemStates.Announce(room, item);
                FurniVisibility.Refresh(room, item);
            }
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

        if (ConfigurationItemStates.IsFlag(item.Definition.InteractionType)) {
            Announce(item);
        }

        if (item.Definition.InteractionType == InteractionType.InvisibleFurniControl) {
            Refresh(item);
        }
    }
    private static void Refresh(Item item)
    {
        if (item.GetRoom() is { } room) {
            FurniVisibility.Refresh(room);
        }
    }
    private static void Announce(Item item)
    {
        if (item.GetRoom() is { } room) {
            ConfigurationItemStates.Announce(room);
        }
    }
}
