using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// State changes Toggle State makes later than its tick batch. Observed on AIR WIN63-202610201032-107964540 (3 runs, 6 firings each): a blob
/// changes state on the next room tick, in a data update of its own, and a button set to 1 goes back to 0 about 600 ms later in a single
/// object data update.
/// </summary>
public sealed class WiredDeferredStates(Room room)
{
    public const long ButtonResetMilliseconds = 600;
    private static readonly ConditionalWeakTable<Room, WiredDeferredStates> Rooms = new();
    private readonly List<Item> _blobs = [];
    private readonly List<(Item Item, long DueMs)> _resets = [];

    public static WiredDeferredStates For(Room room) => Rooms.GetValue(room, current => new(current));

    public static bool IsBlob(Item item) => item.IsFloorItem && item.Definition.ItemName.StartsWith("wf_blob", StringComparison.Ordinal);

    public static bool IsButton(Item item) => item.IsFloorItem && item.Definition.ItemName.StartsWith("wf_button", StringComparison.Ordinal);

    public bool HasPending
    {
        get
        {
            lock (_resets) {
                return _resets.Count > 0;
            }
        }
    }

    public void ToggleOnNextRoomTick(Item blob)
    {
        lock (_resets) {
            if (!_blobs.Contains(blob)) {
                _blobs.Add(blob);
            }
        }
    }

    public void ResetAfterDelay(Item button, long nowMs)
    {
        lock (_resets) {
            _resets.RemoveAll(entry => entry.Item == button);
            _resets.Add((button, nowMs + ButtonResetMilliseconds));
        }
    }

    /// <summary>The blobs queued before this room tick, each turned to its next state.</summary>
    public Item[] TakeBlobs()
    {
        lock (_resets) {
            var taken = _blobs.Where(blob => ReferenceEquals(room.GetRoomItemHandler().GetItem(blob.Id), blob)).ToArray();
            _blobs.Clear();

            return taken;
        }
    }

    public Item[] TakeDueResets(long nowMs)
    {
        lock (_resets) {
            var due = _resets.Where(entry => entry.DueMs <= nowMs).Select(entry => entry.Item).ToArray();
            _resets.RemoveAll(entry => entry.DueMs <= nowMs);

            return [.. due.Where(button => ReferenceEquals(room.GetRoomItemHandler().GetItem(button.Id), button))];
        }
    }

    public static string NextState(Item item)
    {
        var modes = Math.Max(2, item.Definition.Modes);

        return int.TryParse(item.LegacyDataString, out var current) && current >= 0 && current < modes - 1 ? (current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
    }
}
