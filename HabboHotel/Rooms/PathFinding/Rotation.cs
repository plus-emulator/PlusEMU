using System.Drawing;

namespace Plus.HabboHotel.Rooms.PathFinding;

public static class Rotation
{
    public static int Calculate(int x1, int y1, int x2, int y2)
    {
        var rotation = 0;

        if (x1 > x2 && y1 > y2) {
            rotation = 7;
        }
        else if (x1 < x2 && y1 < y2) {
            rotation = 3;
        }
        else if (x1 > x2 && y1 < y2) {
            rotation = 5;
        }
        else if (x1 < x2 && y1 > y2) {
            rotation = 1;
        }
        else if (x1 > x2) {
            rotation = 6;
        }
        else if (x1 < x2) {
            rotation = 2;
        }
        else if (y1 < y2) {
            rotation = 4;
        }
        else if (y1 > y2) {
            rotation = 0;
        }

        return rotation;
    }

    public static int Calculate(int x1, int y1, int x2, int y2, bool moonwalk)
    {
        var rot = Calculate(x1, y1, x2, y2);

        if (!moonwalk) {
            return rot;
        }

        return RotationIverse(rot);
    }

    public static int RotationIverse(int rot)
    {
        if (rot > 3) {
            rot = rot - 4;
        }
        else {
            rot = rot + 4;
        }

        return rot;
    }

    public static Point Offset(int direction) => direction switch
    {
        0 => new(0, -1),
        1 => new(1, -1),
        2 => new(1, 0),
        3 => new(1, 1),
        4 => new(0, 1),
        5 => new(-1, 1),
        6 => new(-1, 0),
        7 => new(-1, -1),
        _ => Point.Empty
    };
}
