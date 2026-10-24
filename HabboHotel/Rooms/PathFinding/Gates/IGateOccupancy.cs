using System.Drawing;

namespace Plus.HabboHotel.Rooms.PathFinding;

// Query port: is any tile of a gate footprint held by an actor or claim?
public interface IGateOccupancy
{
    bool IsBlocked(IReadOnlyList<Point> footprint);

    // Actors in `departing` leave their tile this tick and do not hold it.
    bool IsBlocked(IReadOnlyList<Point> footprint, IReadOnlySet<RoomUser>? departing) => IsBlocked(footprint);
}

// Committed members, off-graph members and every claim kind on any surface of the tile.
internal sealed class ExecutorGateOccupancy(NavGrid grid, ClaimLedger claims) : IGateOccupancy
{
    private const long NoExcludedGroup = long.MinValue;

    public bool IsBlocked(IReadOnlyList<Point> footprint) => IsBlocked(footprint, null);

    public bool IsBlocked(IReadOnlyList<Point> footprint, IReadOnlySet<RoomUser>? departing) => footprint.Any(point => IsTileBlocked(point, departing));

    private bool IsTileBlocked(Point point, IReadOnlySet<RoomUser>? departing)
    {
        if (!grid.InBounds(point.X, point.Y)) {
            return false;
        }

        var tile = grid.Tile(point.X, point.Y);

        if (Held(tile, departing)) {
            return true;
        }

        for (var ordinal = 0; ordinal < grid.SurfaceCount(tile); ordinal++) {
            if (Held(grid.SurfaceAt(tile, ordinal), departing)) {
                return true;
            }
        }

        return false;
    }

    private bool Held(int slot, IReadOnlySet<RoomUser>? departing) => claims.OccupancyAt(slot, NoExcludedGroup, departing) != TargetOccupancy.None;
}
