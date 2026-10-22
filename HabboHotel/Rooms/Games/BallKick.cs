using System.Drawing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Rooms.Games;

public static class BallKick
{
    public static Point? Target(Room room, RoomUser user, Item ball)
    {
        var step = Rotation.Offset(user.RotBody);
        Point target;

        if (user.X == ball.GetX && user.Y == ball.GetY) {
            target = new(ball.GetX + 2 * step.X, ball.GetY + 2 * step.Y);

            if (!room.GetRoomItemHandler().CheckPosItem(ball, target.X, target.Y, ball.Rotation)) {
                target = new(ball.GetX - step.X, ball.GetY - step.Y);
            }
        }
        else if (user.X + step.X == ball.GetX && user.Y + step.Y == ball.GetY) {
            target = new(ball.GetX + step.X, ball.GetY + step.Y);
        }
        else {
            return null;
        }

        return room.GetGameMap().ValidTile(target.X, target.Y) ? target : null;
    }
}
