using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.AreaHide;

public sealed record AreaHideValues(ImmutableArray<int> Values)
{
    public int this[int index] => Values[index];
    public bool On => Values[0] == 1;
}

// The client hides the area. The room owns and persists its eight NumberData settings.
public static class AreaHideState
{
    public const int ControllerMaxSize = 20; // Pinned RoomConfig.AreaHideMaxSize default; only the SetBranding (2955) save checks it.
    // Observed on AIR WIN63-202610201032-107964540: SetAreaHide clamps width and length to 0..64 and does not limit the origin.
    public const int WidgetMaxSize = 64;
    private static readonly ConditionalWeakTable<Item, object> Gates = new();
    private static readonly string[] Fields = ["state", "rootX", "rootY", "width", "length", "invisibility", "wallItems", "invert"];
    public static bool IsAreaHide(Item item) => item.Definition.InteractionType == InteractionType.AreaHide;
    public static IntArrayDataFormat Load(string stored)
    {
        var values = stored.Length == 0 ? [0] : stored.Split('\n').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();

        if (values.Length > 8) {
            throw new FormatException("Invalid area-hide NumberData.");
        }

        return new() { Data = values.Concat(Enumerable.Repeat(0, 8 - values.Length)).ToList() };
    }
    public static bool TryRead(Item item, out AreaHideValues state)
    {
        state = null!;

        if (!IsAreaHide(item) || item.ExtraData is not IntArrayDataFormat data || data.Data.Count != 8) {
            return false;
        }

        state = new(data.Data.ToImmutableArray());

        return true;
    }
    public static bool Configure(Room room, GameClient client, Item item, IReadOnlyList<string> fields)
    {
        if (fields.Count != 16) {
            return false;
        }

        var values = new int[8];
        var seen = new HashSet<int>();

        for (var i = 0; i < fields.Count; i += 2) {
            var index = Array.IndexOf(Fields, fields[i]);

            if (index < 0 || !seen.Add(index) || !int.TryParse(fields[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[index])) {
                return false;
            }
        }

        if (values[0] is not (0 or 1) || values[3] is < 1 or > ControllerMaxSize || values[4] is < 1 or > ControllerMaxSize
            || values[5] is not (0 or 1) || values[6] is not (0 or 1) || values[7] is not (0 or 1)
            || !room.GetGameMap().ValidTile(values[1], values[2])) {
            return false;
        }

        lock (Gates.GetValue(item, _ => new())) {
            if (!ReferenceEquals(client.GetHabbo().CurrentRoom, room) || !room.CheckRights(client) || !Attached(room, item)) {
                return false;
            }

            return Commit(room, item, values);
        }
    }
    /// <summary>
    /// The SetAreaHide save of the widget. Observed on AIR WIN63-202610201032-107964540: the rectangle and options are stored as sent,
    /// negative values become 0 and a width or length over 64 becomes 64. The state is kept.
    /// </summary>
    public static bool Apply(Room room, GameClient client, uint furniId, int x, int y, int width, int length, bool invisibility, bool wallItems, bool invert)
    {
        if (room.GetRoomItemHandler().GetItem(furniId) is not { } item || !IsAreaHide(item)) {
            return false;
        }

        lock (Gates.GetValue(item, _ => new())) {
            if (!ReferenceEquals(client.GetHabbo().CurrentRoom, room) || !room.CheckRights(client) || !Attached(room, item) || !TryRead(item, out var before)) {
                return false;
            }

            return Commit(room, item, [before[0], Math.Max(x, 0), Math.Max(y, 0), Math.Clamp(width, 0, WidgetMaxSize),
                Math.Clamp(length, 0, WidgetMaxSize), invisibility ? 1 : 0, wallItems ? 1 : 0, invert ? 1 : 0]);
        }
    }
    /// <summary>The hiders that are on, in the form the room entry lists them.</summary>
    public static IReadOnlyList<FloorHeightMapComposer.AreaHide> EntryHides(Room room) => On(room)
        .Select(pair => new FloorHeightMapComposer.AreaHide((int)pair.Item.Id, true, pair.State[1], pair.State[2], pair.State[3], pair.State[4], pair.State[7] == 1))
        .ToList();
    public static IEnumerable<(Item Item, AreaHideValues State)> On(Room room, Item? except = null)
    {
        foreach (var item in room.GetRoomItemHandler().GetFloor) {
            if (!ReferenceEquals(item, except) && TryRead(item, out var state) && state.On) {
                yield return (item, state);
            }
        }
    }
    /// <summary>Whether the clients lose this tile while the hider is on: its rectangle, or everything outside it with invert.</summary>
    public static bool Covers(AreaHideValues state, int x, int y)
    {
        var inside = x >= state[1] && x < state[1] + state[3] && y >= state[2] && y < state[2] + state[4];

        return state[7] == 1 ? !inside : inside;
    }
    // Smart indexed variables use source Min=0/Max=int.MaxValue, flags Max=1; controller rectangle bounds are separate.
    public static bool Set(Room room, Item item, int index, int value)
    {
        if (index is < 0 or > 7 || value < 0 || index is 0 or 5 or 6 or 7 && value > 1) {
            return false;
        }

        lock (Gates.GetValue(item, _ => new())) {
            if (!Attached(room, item) || !TryRead(item, out var before)) {
                return false;
            }

            var values = before.Values.ToArray();
            values[index] = value;

            return Commit(room, item, values);
        }
    }
    public static void Toggle(Room room, Item item)
    {
        lock (Gates.GetValue(item, _ => new())) {
            if (Attached(room, item) && TryRead(item, out var state)) {
                Set(room, item, 0, state.On ? 0 : 1);
            }
        }
    }
    private static bool Attached(Room room, Item item) => !item.IsTemporary && item.RoomId == room.Id
        && ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item);
    private static bool Commit(Room room, Item item, int[] values)
    {
        var wasOn = TryRead(item, out var before) && before.On;

        if (!room.GetRoomItemHandler().PersistFurnitureData(item, new IntArrayDataFormat { Data = values.ToList() }, dataUpdateOnly: true)) {
            return false;
        }

        // Observed on AIR WIN63-202610201032-107964540: the data update comes first, then the furni change, then AreaHide, then the
        // tiles. AreaHide is sent only when the hider goes on or off; a rectangle change while on sends none.
        FurniVisibility.RefreshFurni(room);

        if (wasOn != (values[0] == 1)) {
            Announce(room, item);
        }

        FurniVisibility.RefreshTiles(room);

        return true;
    }
    public static void Announce(Room room, Item item, bool removed = false)
    {
        if (TryRead(item, out var state)) {
            room.SendPacket(new AreaHideComposer(item.Id, state, removed));
        }
    }
}
