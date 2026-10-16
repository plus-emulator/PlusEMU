using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>One lease per actual room avatar, shared across boxes. Scheduling stays in the engine.</summary>
public sealed class WiredTemporaryEffects
{
    private sealed record Lease(long Generation, int Original, int Effect, Func<int> Read, Action<int> Write, Func<bool> StillAttached);
    private readonly Dictionary<RoomUser, Lease> _leases = [];
    private long _generation;
    private static readonly ConditionalWeakTable<Room, WiredTemporaryEffects> Rooms = new();
    public static WiredTemporaryEffects For(Room room) => Rooms.GetValue(room, _ => new());

    public Action Acquire(RoomUser user, Func<int> readEffect, Action<int> writeEffect, Func<bool> stillAttached, int effect = 4)
    {
        var current = readEffect();
        var original = _leases.TryGetValue(user, out var previous) && current == previous.Effect ? previous.Original : current;
        var lease = new Lease(++_generation, original, effect, readEffect, writeEffect, stillAttached);
        _leases[user] = lease;
        writeEffect(effect);

        return () =>
        {
            if (!_leases.TryGetValue(user, out var live) || live.Generation != lease.Generation) {
                return;
            }

            Forget(user);
        };
    }

    public void Forget(RoomUser user)
    {
        if (!_leases.Remove(user, out var lease)) {
            return;
        }

        if (lease.StillAttached() && lease.Read() == lease.Effect) {
            lease.Write(lease.Original);
        }
    }

    public void Clear()
    {
        foreach (var user in _leases.Keys.ToArray()) {
            Forget(user);
        }
    }
}
