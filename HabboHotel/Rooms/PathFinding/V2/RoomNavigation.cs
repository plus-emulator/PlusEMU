using Microsoft.Extensions.Logging;
using Plus.Database;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Quests;

namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed partial class RoomNavigation
{
    private readonly Room _room;
    private readonly ActorAccessResolver _access;
    private readonly IDatabase _database;
    private readonly IRewardTrackManager _rewards;
    public PathfindingSettings Settings { get; }
    public NavInputs Inputs { get; }
    public NavGrid Grid { get; }
    public NavGridCompiler Compiler { get; }

    public RoomNavigation(Room room, RoomModel model, PathfindingSettings settings, ILogger<RoomNavigation> logger,
        IGroupManager groups, IDatabase database, IRewardTrackManager rewards)
    {
        _room = room;
        Settings = settings;
        _access = new(groups);
        _database = database;
        _rewards = rewards;

        var width = model.MapSizeX;
        var height = model.MapSizeY;
        var z = new double[width * height];
        var states = new SquareState[z.Length];

        for (var y = 0; y < height; y++) {
            for (var x = 0; x < width; x++) {
                z[y * width + x] = model.SqFloorHeight[x, y];
                states[y * width + x] = model.SqState[x, y];
            }
        }

        Grid = new(width, height, z, states, model.DoorY * width + model.DoorX, model.DoorZ);
        Inputs = new(width, height);
        Compiler = new(Grid, Inputs, settings);
        Compiler.BeforePublish = tiles =>
        {
            if (RoomOwnerScope.IsOwner(_room)) {
                Executor.Context.Geometry.BeforePublish(tiles);
            }
        };
        Inputs.MarkAllDirty();
    }

    public void ApplyDirty()
    {
        try {
            Compiler.ApplyNow();
        }
        catch {
            Inputs.MarkAllDirty();
            throw;
        }
    }
    public void SetFloorStatus(int x, int y, byte status)
    {
        if (!Grid.InBounds(x, y)) {
            return;
        }

        var t = Grid.Tile(x, y);
        Volatile.Write(ref Grid.FloorStatusOverrides[t], status);
        Inputs.MarkDirty(t);
    }

    // The legacy map rewrote this cell from furniture, which ends an explicit floor status there.
    public void ReleaseFloorStatus(int x, int y)
    {
        if (!Grid.InBounds(x, y)) {
            return;
        }

        var t = Grid.Tile(x, y);

        if (Interlocked.Exchange(ref Grid.FloorStatusOverrides[t], -1) != -1) {
            Inputs.MarkDirty(t);
        }
    }

    public void ReleaseFloorStatuses()
    {
        for (var y = 0; y < Grid.Height; y++) {
            for (var x = 0; x < Grid.Width; x++) {
                ReleaseFloorStatus(x, y);
            }
        }
    }
}
