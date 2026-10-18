using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items;

/// <summary>
/// The room-wide switches of ConfigurationItemStates. Invisible furniture are shown while any Invisible Furni Controller is
/// switched on; the :chooser command is disabled for visitors without rights while any :chooser disabler is switched on
/// (AIR ChatInputWidgetHandler ":chooser" checks activeRoomHasChooserDisabled and roomControllerLevel).
/// </summary>
public static class ConfigurationItemStates
{
    public static bool InvisibleFurni(Room room, Item? except = null) => IsOn(room, InteractionType.InvisibleFurniControl, except);
    public static bool ChooserDisabled(Room room, Item? except = null) => IsOn(room, InteractionType.ChooserDisabler, except);
    public static ConfigurationItemStatesComposer Compose(Room room, Item? except = null) =>
        new(ChooserDisabled(room, except), InvisibleFurni(room, except));
    /// <summary>Room entry: the client starts with every switch off, so only a room with a switch on needs the packet.</summary>
    public static void SendTo(GameClient session, Room room)
    {
        // Observed on AIR WIN63-202610151745-318700621: every room entry carries the states, all off included.
        session.Send(Compose(room));
    }
    public static void Announce(Room room, Item? except = null) => room.SendPacket(Compose(room, except));
    private static bool IsOn(Room room, InteractionType type, Item? except) => room.GetRoomItemHandler().GetFloor.Any(item =>
        !ReferenceEquals(item, except) && item.Definition.InteractionType == type && item.LegacyDataString == "1");
}
