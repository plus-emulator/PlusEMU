using Plus.Communication.Packets.Outgoing.Rooms.Engine;

namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed partial class RoomNavigation
{
    private readonly record struct PreviewRequest(int Nonce, int X, int Y);
    private readonly Dictionary<RoomUser, PreviewRequest> _walkPreviews = [];
    private readonly Queue<RoomUser> _previewOrder = new();

    internal void RequestWalkPreview(RoomUser actor, int nonce, int x, int y)
    {
        lock (_walkPreviews) {
            if (!_walkPreviews.ContainsKey(actor)) {
                _previewOrder.Enqueue(actor);
            }

            _walkPreviews[actor] = new(nonce, x, y);
        }
    }

    internal void SendWalkPreviews()
    {
        // A busy stick can replace its own request without delaying other players or their normal walks.
        for (var i = 0; i < 4; i++) {
            PreviewRequest request;
            RoomUser actor;

            lock (_walkPreviews) {
                if (!_previewOrder.TryDequeue(out actor!)) {
                    return;
                }

                _walkPreviews.Remove(actor, out request);
            }

            var session = actor.GetClient();

            if (_room.MDisposed || session == null || session.Closed.IsCancellationRequested
                || session.GetHabbo()?.CurrentRoom != _room || actor.Movement.State != NavState.Active
                || _room.GetRoomUserManager()?.GetRoomUserByVirtualId(actor.VirtualId) != actor) {
                continue;
            }

            var route = PreviewWalk(actor, request.X, request.Y);
            session.Send(new WalkPreviewComposer(request.Nonce, (int)_room.RoomId, request.X, request.Y, actor.X, actor.Y, route));
        }
    }

    internal (int X, int Y)[]? PreviewWalk(RoomUser actor, int x, int y)
    {
        if (!actor.CanWalk || !Grid.InBounds(x, y)) {
            return [];
        }

        var context = Executor.Context;
        var profile = context.Profiles.Refresh(actor);
        var occupancy = context.Occupancy(actor);
        var start = new NavPosition(actor.X, actor.Y, actor.Movement.SupportZ);
        var goal = GoalResolver.ResolveClick(Grid, profile, start, x, y, occupancy);

        if (goal.Count == 0 || goal.X != x || goal.Y != y) {
            return [];
        }

        var settings = Settings with { MaxExpansionsPerSearch = Math.Min(Settings.MaxExpansionsPerSearch ?? 4096, 4096) };
        var route = new Route();
        using var lease = PathWorkspacePool.Rent(Grid.SlotCapacity, profile.LegacyOverride ? Grid.SlotCapacity : Grid.ActiveNodeCount);
        var outcome = new PathSearch(Grid, settings).Find(new(profile, start, x, y, occupancy, goal, View: OccupancyView.InitialPlanning),
            lease.Workspace, route);

        if (outcome == PathOutcome.BudgetCancelled || route.Count > 512) {
            return null;
        }

        if (outcome != PathOutcome.Found) {
            return [];
        }

        var tiles = new (int X, int Y)[route.Count];

        for (var i = 0; i < tiles.Length; i++) {
            var tile = route.Steps[i].Tile;
            tiles[i] = (tile % Grid.Width, tile / Grid.Width);
        }

        return tiles;
    }
}
