using System.Runtime.CompilerServices;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.AreaHide;

namespace Plus.HabboHotel.Rooms;

/// <summary>
/// The floor furni a room keeps off its clients. Observed on AIR WIN63-202610201032-107964540: while the Invisible Furni Controller is
/// on, the clients get no room_invisible furni and no area hider with the invisibility option. The server removes them with one
/// ObjectRemoveMultiple (picker -1, flag on) and adds them again with one Objects packet when the controller goes off. The room keeps
/// the set the clients were told about, so the diff of the next refresh is exactly what changed.
/// </summary>
public static class FurniVisibility
{
    private sealed class Concealed
    {
        public readonly HashSet<uint> Ids = [];
    }

    private static readonly ConditionalWeakTable<Room, Concealed> Rooms = new();
    private const string InvisiblePrefix = "room_invisible_";

    public static bool IsInvisibleClass(Item item) => item.Definition.ItemName.StartsWith(InvisiblePrefix, StringComparison.Ordinal)
        || AreaHideState.TryRead(item, out var area) && area[5] == 1;

    /// <summary>Whether the clients should not have this floor item now. <paramref name="except"/> is an item being taken out.</summary>
    public static bool ShouldHide(Room room, Item item, Item? except = null) => item.IsFloorItem && IsInvisibleClass(item)
        && ConfigurationItemStates.InvisibleFurni(room, except);

    /// <summary>Whether the clients were told to drop this item (or never got it).</summary>
    public static bool IsConcealed(Room room, Item item)
    {
        var state = Rooms.GetValue(room, _ => new());

        lock (state) {
            return state.Ids.Contains(item.Id);
        }
    }

    /// <summary>Records a new item that is hidden on arrival. Returns true when the clients must not get it.</summary>
    public static bool ConcealOnArrival(Room room, Item item)
    {
        if (!ShouldHide(room, item)) {
            return false;
        }

        var state = Rooms.GetValue(room, _ => new());

        lock (state) {
            state.Ids.Add(item.Id);
        }

        return true;
    }

    /// <summary>Forgets a removed item. Returns true when the clients never had it, so no remove packet is due.</summary>
    public static bool ForgetRemoved(Room room, Item item)
    {
        var state = Rooms.GetValue(room, _ => new());

        lock (state) {
            return state.Ids.Remove(item.Id);
        }
    }

    /// <summary>A room entry lists no hidden furni, so the room counts them as told. Nothing is sent.</summary>
    public static void Seed(Room room)
    {
        var state = Rooms.GetValue(room, _ => new());
        var hidden = room.GetRoomItemHandler().GetFloor.Where(item => ShouldHide(room, item)).Select(item => item.Id).ToList();

        lock (state) {
            state.Ids.UnionWith(hidden);
        }
    }

    public static void Refresh(Room room, Item? except = null)
    {
        var state = Rooms.GetValue(room, _ => new());
        var floor = room.GetRoomItemHandler().GetFloor.Where(item => !ReferenceEquals(item, except)).ToList();
        var hidden = floor.Where(item => ShouldHide(room, item, except)).ToList();
        List<uint> removed;
        List<Item> added;

        lock (state) {
            removed = hidden.Select(item => item.Id).Where(state.Ids.Add).Order().ToList();
            var now = hidden.Select(item => item.Id).ToHashSet();
            added = floor.Where(item => state.Ids.Contains(item.Id) && !now.Contains(item.Id)).ToList();
            state.Ids.RemoveWhere(id => !now.Contains(id));
        }

        if (removed.Count > 0) {
            room.SendPacket(new ObjectRemoveMultipleComposer(removed, -1, true));
        }

        if (added.Count > 0) {
            var snapshot = RoomFurnitureSnapshot.Capture(added, room.OwnerId, room.OwnerName);
            room.SendPacket(new ObjectsComposer(snapshot));
            room.SendPacket(new FurnitureMetadataComposer(snapshot.Items));
        }
    }
}
