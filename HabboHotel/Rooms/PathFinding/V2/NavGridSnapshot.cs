namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed partial class NavGrid
{
    public NavGrid CreateSnapshot() => new(this);

    private NavGrid(NavGrid source)
    {
        Width = source.Width;
        Height = source.Height;
        DoorTile = source.DoorTile;
        DoorZ = source.DoorZ;
        Version = source.Version;
        ActiveNodeCount = source.ActiveNodeCount;
        Layered = source.Layered;
        _overflowHighWater = source._overflowHighWater;
        BaseZ = (double[])source.BaseZ.Clone();
        BaseState = (SquareState[])source.BaseState.Clone();
        WalkZ = (double[])source.WalkZ.Clone();
        LegacyZ = (double[])source.LegacyZ.Clone();
        Flags = (NavFlags[])source.Flags.Clone();
        SupportItem = (uint[])source.SupportItem.Clone();
        Ordinal = (byte[])source.Ordinal.Clone();
        Kind = (SurfaceKind[])source.Kind.Clone();
        GroupId = (int[])source.GroupId.Clone();
        PillowTiles = CloneRows(source.PillowTiles);
        TileVoid = (bool[])source.TileVoid.Clone();
        FloorLocks = (int[])source.FloorLocks.Clone();
        FloorStatusOverrides = (int[])source.FloorStatusOverrides.Clone();
        LegacyFloorStatus = (byte[])source.LegacyFloorStatus.Clone();
        _tileSlots = (int[])source._tileSlots.Clone();
        _tileSurfaceCount = (byte[])source._tileSurfaceCount.Clone();
        _overflowTile = (int[])source._overflowTile.Clone();
        _contacts = CloneRows(source._contacts);
        _freeOverflow.UnionWith(source._freeOverflow);
        _forcedOffGraph.UnionWith(source._forcedOffGraph);

        foreach (var entry in source._releasedSlots) {
            _releasedSlots.Add(entry.Key, entry.Value);
        }

        foreach (var entry in source._leftPrimaries) {
            _leftPrimaries.Add(entry.Key, entry.Value);
        }

        Connectivity = new(this);
    }

    private static T[][] CloneRows<T>(T[][] rows)
        => Array.ConvertAll(rows, row => row == null ? null! : (T[])row.Clone());
}

// Commands retain their snapshot; the cache retains only the latest published version.
internal sealed class PlanningGridCache(NavGrid grid)
{
    private NavGrid? _snapshot;
    internal NavGrid Capture()
    {
        if (_snapshot == null || _snapshot.Version != grid.Version) {
            _snapshot = grid.CreateSnapshot();
        }

        return _snapshot;
    }
}
