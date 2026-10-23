using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items;

/// <summary>
/// The room-wide switches of ConfigurationItemStates. Invisible furniture are hidden from the clients while an Invisible Furni Controller is on.
/// The hand item drop is blocked while a hand item blocker is on.
/// The :chooser command is disabled for visitors without rights while a :chooser disabler is on.
/// </summary>
public static class ConfigurationItemStates
{
    public static bool InvisibleFurni(Room room, Item? except = null) => IsOn(room, InteractionType.InvisibleFurniControl, except);
    public static bool ChooserDisabled(Room room, Item? except = null) => IsOn(room, InteractionType.ChooserDisabler, except);
    public static bool HandItemBlocked(Room room, Item? except = null) => IsOn(room, InteractionType.HandItemBlocker, except);
    public static bool WiredDisabled(Room room) => IsOn(room, InteractionType.WiredDisabler, null);
    public const int DefaultRollerStepMilliseconds = 2000;
    public static Item? RollerSpeedController(Room room) => room.GetRoomItemHandler()?.GetFloor
        .FirstOrDefault(item => item.Definition.InteractionType == InteractionType.RollerSpeedController);
    /// <summary>
    /// The time between two roller steps in the room, in milliseconds. A room without the speed controller steps every 2000 ms.
    /// The controller has 12 states in four groups of three: 500, 1000, 1500 and 2000 ms.
    /// </summary>
    public static int RollerStepMilliseconds(Room room) => RollerSpeedController(room) is { } controller
        ? InteractorRollerSpeed.StepMilliseconds(controller)
        : DefaultRollerStepMilliseconds;
    public static ConfigurationItemStatesComposer Compose(Room room, Item? except = null) =>
        new(ChooserDisabled(room, except), InvisibleFurni(room, except), HandItemBlocked(room, except));
    /// <summary>Room entry: every entry carries the states, all off included.</summary>
    public static void SendTo(GameClient session, Room room)
    {
        session.Send(Compose(room));
    }
    /// <summary>The furni whose state is one of the room flags in the flags packet.</summary>
    public static bool IsFlag(InteractionType type) => type is InteractionType.InvisibleFurniControl or InteractionType.ChooserDisabler
        or InteractionType.HandItemBlocker;
    /// <summary>A room holds one of each configuration furni. A second placement is refused with the type limit text.</summary>
    public static bool IsLimitedToOne(InteractionType type) => IsFlag(type) || type is InteractionType.WiredDisabler or InteractionType.RollerSpeedController;
    public static bool TypeLimitReached(Room room, ItemDefinition definition) => IsLimitedToOne(definition.InteractionType)
        && room.GetRoomItemHandler().GetFloor.Any(item => item.Definition.InteractionType == definition.InteractionType);
    public static void SendTypeLimit(GameClient session) =>
        session.Send(new RoomNotificationComposer("furni_placement_error", "message", "${room.error.type_limit}"));
    public static void Announce(Room room, Item? except = null) => room.SendPacket(Compose(room, except));

    /// <summary>
    /// A flag furni has a new state: the room flags go out, and the invisible furni are hidden or shown again.
    /// A switch by Wired does the same as a use.
    /// </summary>
    public static void StateChanged(Item item)
    {
        if (!IsFlag(item.Definition.InteractionType) || item.GetRoom() is not { } room) {
            return;
        }

        Announce(room);

        if (item.Definition.InteractionType == InteractionType.InvisibleFurniControl) {
            FurniVisibility.Refresh(room);
        }
    }
    private static bool IsOn(Room room, InteractionType type, Item? except) => room.GetRoomItemHandler()?.GetFloor.Any(item =>
        !ReferenceEquals(item, except) && item.Definition.InteractionType == type && item.LegacyDataString == "1") == true;
}
