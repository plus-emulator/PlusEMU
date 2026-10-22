using System.Drawing;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Rooms.Games.Banzai;

internal static class BanzaiEnclosure
{
    private static readonly Size[] Neighbours = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    // Floods in from outside the arena through every tile that is not the team's own lock; free tiles it cannot reach are enclosed.
    public static IReadOnlyList<Point> EnclosedTiles(IReadOnlySet<Point> arena, IReadOnlyDictionary<Point, Team> lockedBy, Team team)
    {
        bool IsWall(Point tile) => lockedBy.TryGetValue(tile, out var owner) && owner == team;

        var reached = new HashSet<Point>();
        var open = new Queue<Point>();

        foreach (var tile in arena) {
            if (!IsWall(tile) && Neighbours.Any(step => !arena.Contains(tile + step)) && reached.Add(tile)) {
                open.Enqueue(tile);
            }
        }

        while (open.TryDequeue(out var tile)) {
            foreach (var step in Neighbours) {
                var next = tile + step;

                if (arena.Contains(next) && !IsWall(next) && reached.Add(next)) {
                    open.Enqueue(next);
                }
            }
        }

        return [.. arena.Where(tile => !reached.Contains(tile) && !lockedBy.ContainsKey(tile))];
    }
}
