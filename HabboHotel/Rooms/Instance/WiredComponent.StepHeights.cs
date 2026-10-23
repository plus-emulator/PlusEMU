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

    // Runs when a fast pass starts and when the room tick has sent its UserUpdate. A move outside a pass (speech, a state change), in a fast
    // pass, or before the step was announced must not reach the room tick's resend.
    private void ForgetRestackedSteps()
    {
        lock (_movedFurniTiles) {
            _movedFurniTiles.Clear();
            _stepStarts = null;
        }
    }

    // Runs only at the end of the room tick pass, after its WiredMovements, in the batch that announced the step. A resend later in the step
    // would restart it from its start tile, so the client snaps the avatar back. A second move for an avatar in the same batch restarts from the same start,
    // so at most one frame shows the old height.
    private void ResendRestackedSteps()
    {
        HashSet<(int X, int Y)> tiles;
        Dictionary<int, RoomUserStatusSnapshot> starts;

        lock (_movedFurniTiles) {
            tiles = [.. _movedFurniTiles];
            starts = _stepStarts ?? [];
            _movedFurniTiles.Clear();
            _stepStarts = null;
        }

        if (tiles.Count == 0 || _room.GetGameMap()?.Navigation is not { } navigation) {
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
