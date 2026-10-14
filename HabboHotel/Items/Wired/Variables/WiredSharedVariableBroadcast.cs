using System.Collections.Concurrent;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// "WIRED Variable: From Another Room": a room that changes a shared variable through its reference tells the room the variable
/// lives in, whose "variable changed" triggers can allow changes from another room (the editor's "Another room" origin). Rooms are
/// held weakly; a room that is not loaded has no triggers to fire and hears nothing.
/// </summary>
public static class WiredSharedVariableBroadcast
{
    /// <summary>A loaded room's end of the broadcast: its module and how it finds a player (by id) among its own units.</summary>
    public sealed class Endpoint(WiredVariableModule module, Func<long, int> unitOf)
    {
        public WiredVariableModule Module { get; } = module;
        public Func<long, int> UnitOf { get; } = unitOf;
    }

    private static readonly ConcurrentDictionary<uint, WeakReference<Endpoint>> Rooms = new();

    /// <summary>The caller keeps the endpoint alive for as long as the room is loaded.</summary>
    public static void Register(Endpoint endpoint) => Rooms[endpoint.Module.RoomId] = new(endpoint);

    private static Endpoint? Find(uint roomId) => Rooms.TryGetValue(roomId, out var weak) && weak.TryGetTarget(out var endpoint) ? endpoint : null;

    /// <summary>
    /// Hands each change a room drained to the room its variable lives in, when that is another room. A user's value was named by
    /// the writer room's unit index, so it is renamed to the unit of the same player there. Changes that came from another room are
    /// never passed on again. Returns the changes delivered.
    /// </summary>
    public static int Deliver(uint writerRoomId, IEnumerable<WiredVariableChange> changes)
    {
        var delivered = 0;

        foreach (var change in changes) {
            if (!change.Shared || change.Origin == 3 || change.RoomId == writerRoomId || Find(change.RoomId) is not { } home) {
                continue;
            }

            home.Module.EnqueueForeign(change.Key.Target == WiredVariableTarget.User ? change with { EntityId = home.UnitOf(change.Key.HolderId) } : change);
            delivered++;
        }

        return delivered;
    }
}
