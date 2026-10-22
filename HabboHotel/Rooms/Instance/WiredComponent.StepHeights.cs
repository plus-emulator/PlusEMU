using Plus.Communication.Packets.Outgoing.Rooms.Engine;

namespace Plus.HabboHotel.Rooms.Instance;

// Deliberate difference from Habbo: Habbo keeps the step height it announced before a Wired move raised or lowered the step's
// tile, so the avatar dips during the step. Plus sends the step again in the same pass, so the new height lands with the step.
public partial class WiredComponent
{
    private readonly HashSet<(int X, int Y)> _movedFurniTiles = [];
    private Dictionary<int, RoomUserStatusSnapshot>? _stepStarts;

    /// <summary>Keeps where each walking avatar's step starts, as its last status showed it, before a Wired furni move rebinds it.</summary>
    internal void FurniMoving()
    {
        lock (_movedFurniTiles) {
            _stepStarts ??= RoomUserStatusSnapshot.Capture(_room.GetRoomUserManager().GetUserList().Where(user => user.IsWalking && user.SetStep))
                .ToDictionary(snapshot => snapshot.VirtualId);
        }
    }

    /// <summary>Notes the tiles a Wired furni move left and covered in this pass.</summary>
    internal void FurniMoved(IEnumerable<(int X, int Y)> tiles)
    {
        lock (_movedFurniTiles) {
            _movedFurniTiles.UnionWith(tiles);
        }
    }

    // Runs at the end of the pass, after its WiredMovements. AIR applies the packets of one burst in one frame, and a second move for an
    // avatar restarts from the same start point (MovingObjectLogic.as:160-189), so only the corrected height shows.
    private void ResendRestackedSteps()
    {
        (int X, int Y)[] tiles;
        Dictionary<int, RoomUserStatusSnapshot> starts;

        lock (_movedFurniTiles) {
            tiles = [.. _movedFurniTiles];
            starts = _stepStarts ?? [];
            _movedFurniTiles.Clear();
            _stepStarts = null;
        }

        if (tiles.Length == 0 || _room.GetGameMap()?.Navigation is not { } navigation) {
            return;
        }

        var stepping = _room.GetRoomUserManager().GetUserList()
            .Where(user => user.IsWalking && user.SetStep && tiles.Contains((user.SetX, user.SetY))).ToArray();
        var raised = navigation.RefreshStepHeights(stepping);

        if (raised.Length > 0) {
            _room.SendPacket(new UserUpdateComposer([.. RoomUserStatusSnapshot.Capture(raised).Select(snapshot =>
                starts.TryGetValue(snapshot.VirtualId, out var start) && (start.X, start.Y) == (snapshot.X, snapshot.Y)
                    ? start with { Status = snapshot.Status } : snapshot)]));
        }
    }
}
