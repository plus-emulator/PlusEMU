using System.Drawing;

namespace Plus.HabboHotel.Rooms.PathFinding;

public static class InteractionMovement
{
    public static void RequestInteractionStep(this RoomUser actor, Room room, Point target)
    {
        actor.AllowOverride = false;
        room.GetGameMap().Navigation.InteractionStep(actor, target.X, target.Y);
    }
}
