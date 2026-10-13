using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items;

/// <summary>Invisible furniture are shown while any Invisible Furni Controller in the room is switched on.</summary>
public static class InvisibleFurniState
{
    public static bool IsOn(Room room, Item? except = null) => room.GetRoomItemHandler().GetFloor.Any(item =>
        !ReferenceEquals(item, except) && item.Definition.InteractionType == InteractionType.InvisibleFurniControl && item.LegacyDataString == "1");
    public static void SendTo(GameClient session, Room room)
    {
        if (IsOn(room)) {
            session.Send(new ConfigurationItemStatesComposer(true));
        }
    }
    public static void Announce(Room room, Item? except = null) => room.SendPacket(new ConfigurationItemStatesComposer(IsOn(room, except)));
}
