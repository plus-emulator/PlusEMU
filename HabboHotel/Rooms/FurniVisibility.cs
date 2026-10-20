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
        public readonly HashSet<(int X, int Y)> Tiles = [];
    }

    private static readonly ConditionalWeakTable<Room, Concealed> Rooms = new();
    private const string InvisiblePrefix = "room_invisible_";

    public static bool IsInvisibleClass(Item item) => item.Definition.ItemName.StartsWith(InvisiblePrefix, StringComparison.Ordinal)
        || AreaHideState.TryRead(item, out var area) && area[5] == 1;

    /// <summary>Whether the clients should not have this floor item now. <paramref name="except"/> is an item being taken out.</summary>
    public static bool ShouldHide(Room room, Item item, Item? except = null) => item.IsFloorItem
        && (IsInvisibleClass(item) && ConfigurationItemStates.InvisibleFurni(room, except) || InHiddenArea(room, item, except));

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: a hider that is on removes the floor furni on its tiles from the clients (outside its
    /// rectangle with invert). The hider itself and the avatars stay.
    /// </summary>
    private static bool InHiddenArea(Room room, Item item, Item? except)
    {
        if (AreaHideState.IsAreaHide(item)) {
            return false;
        }

        var hides = AreaHideState.On(room, except).Select(pair => pair.State).ToList();

        return hides.Count > 0 && item.GetCoords.Any(tile => hides.Any(state => AreaHideState.Covers(state, tile.X, tile.Y)));
    }

    private static HashSet<(int X, int Y)> HiddenTiles(Room room, Item? except)
    {
        var hides = AreaHideState.On(room, except).Select(pair => pair.State).ToList();
        var tiles = new HashSet<(int X, int Y)>();

        if (hides.Count == 0) {
            return tiles;
        }

        var map = room.GetGameMap();

        for (var y = 0; y < map.StaticModel.MapSizeY; y++) {
            for (var x = 0; x < map.StaticModel.MapSizeX; x++) {
                if (map.StaticModel.SqState[x, y] != SquareState.Blocked && hides.Any(state => AreaHideState.Covers(state, x, y))) {
                    tiles.Add((x, y));
                }
            }
        }

        return tiles;
    }

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
        RefreshFurni(room, except);
        RefreshTiles(room, except);
    }

    /// <summary>
    /// Sends the height map update for the tiles that the hiders took or gave back: 0xFFFF for a hidden tile, the real value otherwise.
    /// </summary>
    public static void RefreshTiles(Room room, Item? except = null)
    {
        var state = Rooms.GetValue(room, _ => new());
        var desired = HiddenTiles(room, except);
        List<(int X, int Y)> taken;
        List<(int X, int Y)> given;

        lock (state) {
            taken = desired.Except(state.Tiles).ToList();
            given = state.Tiles.Except(desired).ToList();
            state.Tiles.Clear();
            state.Tiles.UnionWith(desired);
        }

        var map = room.GetGameMap();
        var updates = taken.Select(tile => new HeightMapUpdateComposer.Tile(tile.X, tile.Y, -1))
            .Concat(given.Select(tile => new HeightMapUpdateComposer.Tile(tile.X, tile.Y, map.ClientTileValue(tile.X, tile.Y))))
            .OrderBy(tile => tile.Y).ThenBy(tile => tile.X).ToList();

        for (var start = 0; start < updates.Count; start += 255) {
            room.SendPacket(new HeightMapUpdateComposer(updates.Skip(start).Take(255).ToList()));
        }
    }

    public static void RefreshFurni(Room room, Item? except = null)
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
