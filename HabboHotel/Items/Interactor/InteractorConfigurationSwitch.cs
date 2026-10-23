using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// Room-wide configuration switches (Invisible Furni Controller, :chooser disabler, hand item blocker, wired disabler): a rights holder
/// or a wired toggle flips the state between "0" and "1". The first three are room flags, so every change re-announces the flags.
/// </summary>
public sealed class InteractorConfigurationSwitch(TimeProvider? clock = null) : IFurniInteractor
{
    /// <summary>Several uses of one switch at the same moment switch it once, while uses 100 ms apart each count. The first use
    /// counts; the window is one room tick.</summary>
    private static readonly TimeSpan UseWindow = TimeSpan.FromMilliseconds(50);
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public void OnPlace(GameClient? session, Item item)
    {
        // Placing the Invisible Furni Controller sends the flags; the other furni send nothing.
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

        // A flag furni taken up while on sends the flag still on, so the client keeps it until the next room entry; taken up while off it sends nothing.
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
        if (session == null || !hasRights) {
            return;
        }

        var now = _clock.GetUtcNow();

        if (item.LastSwitchUse is { } last && now - last < UseWindow) {
            return;
        }

        item.LastSwitchUse = now;
        Toggle(item, used: true);
    }
    public void OnWiredTrigger(Item item)
    {
        // Wired cannot switch the wired disabler back off.
        if (item.Definition.InteractionType == InteractionType.WiredDisabler && item.LegacyDataString == "1") {
            return;
        }

        Toggle(item, used: false);
    }
    private static void Toggle(Item item, bool used)
    {
        item.LegacyDataString = item.LegacyDataString == "1" ? "0" : "1";

        // Using any of these furni sends one ObjectDataUpdate with the new state, then the room flags (not for the wired disabler), and no ObjectUpdate.
        if (used) {
            item.UpdateUsedState();
            ConfigurationItemStates.StateChanged(item);

            return;
        }

        // A switch by Wired keeps its packets.
        item.UpdateState(true, item.Definition.InteractionType != InteractionType.ChooserDisabler);
        ConfigurationItemStates.StateChanged(item);
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
