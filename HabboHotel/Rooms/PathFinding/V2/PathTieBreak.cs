namespace Plus.HabboHotel.Rooms.PathFinding;

public static class PathTieBreak
{
    private static readonly (int X, int Y)[] Offsets = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];
    public static ReadOnlySpan<(int X, int Y)> Neighbours => Offsets;
    public static int Heuristic(int x, int y, int goalX, int goalY) => Math.Max(Math.Abs(x - goalX), Math.Abs(y - goalY));
    public static UInt128 Key(long f, int x, int y, int goalX, int goalY, byte ordinal, int sequence)
    {
        var dx = (long)x - goalX;
        var dy = (long)y - goalY;
        var tie = dx * dx + dy * dy;
        System.Diagnostics.Debug.Assert(f >= 0 && f <= uint.MaxValue && ordinal < 4 && sequence >= 0);

        return (UInt128)f << 96 | (UInt128)tie << 33 | (UInt128)ordinal << 31 | (uint)sequence;
    }
}
