using System.Collections.Concurrent;

namespace Plus.HabboHotel.Items;

/// <summary>
/// The id band the client reserves for Builders Club furni. The client tells a Builders Club furni only by its object id, then
/// colours the infostand and names the warehouse instead of the owner.
/// A Builders Club furni keeps its stored id inside the emulator. On the wire it carries an id from this band: composers write
/// <see cref="ToWire"/> and room lookups accept <see cref="FromWire"/>.
/// </summary>
public static class BuildersClubItemIds
{
    public const uint First = 2147418112;
    public const uint Last = 2147483647;

    private static readonly ConcurrentDictionary<uint, uint> WireByItem = new();
    private static readonly ConcurrentDictionary<uint, uint> ItemByWire = new();
    private static readonly Lock Sync = new();
    private static uint _next = First;

    public static bool Contains(uint id) => id is >= First and <= Last;

    // Gives the stored Builders Club furni its band id; the same furni keeps the id until it is released.
    public static uint Assign(uint itemId)
    {
        if (WireByItem.TryGetValue(itemId, out var wire)) {
            return wire;
        }

        lock (Sync) {
            if (WireByItem.TryGetValue(itemId, out wire)) {
                return wire;
            }

            for (var tried = 0u; tried <= Last - First; tried++) {
                var candidate = _next;
                _next = _next == Last ? First : _next + 1;

                if (ItemByWire.TryAdd(candidate, itemId)) {
                    WireByItem[itemId] = candidate;

                    return candidate;
                }
            }
        }

        throw new InvalidOperationException("The Builders Club id band is full.");
    }

    // Frees the band id of a deleted Builders Club furni.
    public static void Release(uint itemId)
    {
        lock (Sync) {
            if (WireByItem.TryRemove(itemId, out var wire)) {
                ItemByWire.TryRemove(wire, out _);
            }
        }
    }

    // The id the client knows the furni by.
    public static uint ToWire(uint itemId) => TemporaryItemIds.IsWallIdentity(itemId) ? TemporaryItemIds.ToWire(itemId) : !WireByItem.IsEmpty && WireByItem.TryGetValue(itemId, out var wire) ? wire : itemId;

    public static int ToWire(int itemId) => TemporaryItemIds.IsWallIdentity(unchecked((uint)itemId)) ? unchecked((int)TemporaryItemIds.ToWire(unchecked((uint)itemId))) : itemId > 0 ? unchecked((int)ToWire((uint)itemId)) : itemId;

    // The stored id behind an id the client sent.
    public static uint FromWire(uint id) => Contains(id) && ItemByWire.TryGetValue(id, out var itemId) ? itemId : id;

    public static int FromWire(int id) => id > 0 ? unchecked((int)FromWire((uint)id)) : id;
}
