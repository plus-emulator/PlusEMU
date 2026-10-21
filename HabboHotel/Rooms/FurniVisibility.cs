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
    private static int _scans;

    /// <summary>How often the room was scanned for hiders. Tests read it to check that one call scans once.</summary>
    internal static int Scans => Volatile.Read(ref _scans);

    private const string InvisiblePrefix = "room_invisible_";

    public static bool IsInvisibleClass(Item item) => item.Definition.ItemName.StartsWith(InvisiblePrefix, StringComparison.Ordinal)
        || AreaHideState.TryRead(item, out var area) && area[5] == 1;

    /// <summary>What hides furni right now: the Invisible Furni Controller and the hiders that are on. One scan of the room.</summary>
    internal readonly record struct Hiding(bool InvisibleOn, IReadOnlyList<AreaHideValues> Areas)
    {
        public bool Any => InvisibleOn || Areas.Count > 0;
    }

    /// <summary><paramref name="except"/> is an item being taken out.</summary>
    internal static Hiding Capture(Room room, Item? except = null)
    {
        Interlocked.Increment(ref _scans);

        return new(ConfigurationItemStates.InvisibleFurni(room, except), AreaHideState.On(room, except).Select(pair => pair.State).ToList());
    }

    /// <summary>Whether the clients should not have this floor item now.</summary>
    internal static bool Hides(Hiding hiding, Item item) => item.IsFloorItem
        && (hiding.InvisibleOn && IsInvisibleClass(item) || InHiddenArea(hiding, item));

    /// <summary>Whether the clients should not have this floor item now. <paramref name="except"/> is an item being taken out.</summary>
    public static bool ShouldHide(Room room, Item item, Item? except = null) => Hides(Capture(room, except), item);

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: a hider that is on removes the floor furni on its tiles from the clients (outside its
    /// rectangle with invert). The hider itself and the avatars stay.
    /// </summary>
    private static bool InHiddenArea(Hiding hiding, Item item)
    {
        if (hiding.Areas.Count == 0 || AreaHideState.IsAreaHide(item)) {
            return false;
        }

        return item.GetCoords.Any(tile => hiding.Areas.Any(state => AreaHideState.Covers(state, tile.X, tile.Y)));
    }

    /// <summary>The tiles the hiders that are on take from the clients.</summary>
    internal static HashSet<(int X, int Y)> HiddenTiles(Room room, Item? except = null) => HiddenTiles(room, Capture(room, except).Areas);

    private static HashSet<(int X, int Y)> HiddenTiles(Room room, IReadOnlyList<AreaHideValues> hides)
    {
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

    /// <summary>A room entry lists no hidden furni and marks the hidden tiles, so the room counts them as told. Nothing is sent.</summary>
    public static void Seed(Room room)
    {
        var state = Rooms.GetValue(room, _ => new());
        var hiding = Capture(room);
        var hidden = hiding.Any ? room.GetRoomItemHandler().GetFloor.Where(item => Hides(hiding, item)).Select(item => item.Id).ToList() : [];
        var tiles = HiddenTiles(room, hiding.Areas);

        lock (state) {
            state.Ids.UnionWith(hidden);
            state.Tiles.Clear();
            state.Tiles.UnionWith(tiles);
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
        var hiding = Capture(room, except);
        var hidden = hiding.Any ? floor.Where(item => Hides(hiding, item)).ToList() : [];
        List<uint> removed;
        List<Item> added;

        lock (state) {
            removed = hidden.Select(item => item.Id).Where(state.Ids.Add).Order().ToList();
            var now = hidden.Select(item => item.Id).ToHashSet();
            // A wired box that the room keeps hidden stays hidden when its area goes off.
            added = floor.Where(item => state.Ids.Contains(item.Id) && !now.Contains(item.Id) && !(room.HideWired && item.IsWired)).ToList();
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

    /// <summary>
    /// Brings the clients in line after floor furni moved: one that moved into a hidden area is removed, one that left it is added.
    /// Returns the ids that need no ObjectUpdate, because they are hidden now or were just sent whole.
    /// </summary>
    internal static HashSet<uint> Reconcile(Room room, IReadOnlyCollection<Item> moved)
    {
        var state = Rooms.GetValue(room, _ => new());
        var settled = new HashSet<uint>();
        var hiding = Capture(room);
        var removed = new List<uint>();
        var added = new List<Item>();

        lock (state) {
            foreach (var item in moved.Where(item => item.IsFloorItem)) {
                var hidden = Hides(hiding, item);
                var told = state.Ids.Contains(item.Id);

                if (hidden && !told) {
                    state.Ids.Add(item.Id);
                    removed.Add(item.Id);
                }
                else if (!hidden && told) {
                    state.Ids.Remove(item.Id);
                    added.Add(item);
                }
                else if (!hidden) {
                    continue;
                }

                settled.Add(item.Id);
            }
        }

        if (removed.Count > 0) {
            room.SendPacket(new ObjectRemoveMultipleComposer(removed.Order().ToList(), -1, true));
        }

        added = added.Where(item => !(room.HideWired && item.IsWired)).ToList();

        if (added.Count > 0) {
            var snapshot = RoomFurnitureSnapshot.Capture(added, room.OwnerId, room.OwnerName);
            room.SendPacket(new ObjectsComposer(snapshot));
            room.SendPacket(new FurnitureMetadataComposer(snapshot.Items));
        }

        return settled;
    }
}
