using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// State changes Toggle State makes later than its tick batch: a blob changes on the next room tick, in a data update of its own, and a button
/// set to 1 goes back to 0 after 600 ms. Only the classes named here behave this way. Every blob goes to 0 on the room tick after a game counter starts.
/// </summary>
public sealed class WiredDeferredStates(Room room)
{
    public const long ButtonResetMilliseconds = 600;
    private static readonly ConditionalWeakTable<Room, WiredDeferredStates> Rooms = new();
    private readonly List<Item> _blobs = [];
    private readonly List<(Item Item, long DueMs)> _resets = [];
    private bool _reported;
    private bool _resetBlobs;

    public static WiredDeferredStates For(Room room) => Rooms.GetValue(room, current => new(current));

    public static bool IsBlob(Item item) => item.IsFloorItem && item.Definition.ItemName is "wf_blob" or "wf_blob2";

    public static bool IsButton(Item item) => item.IsFloorItem && item.Definition.ItemName is "wf_button" or "wf_button_red" or "wf_button_x";

    public bool HasPending
    {
        get
        {
            lock (_resets) {
                return _resets.Count > 0 || _reported;
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

    public void ResetBlobsOnNextRoomTick()
    {
        lock (_resets) {
            _resetBlobs = true;
        }
    }

    public void CancelReset(Item button)
    {
        lock (_resets) {
            _resets.RemoveAll(entry => entry.Item == button);
        }
    }

    public void ResetAfterDelay(Item button, long nowMs)
    {
        lock (_resets) {
            _resets.RemoveAll(entry => entry.Item == button);
            _resets.Add((button, nowMs + ButtonResetMilliseconds));
        }
    }

    /// <summary>Every blob of the room that is not queued for a toggle in this tick, when a game counter started before this room tick, otherwise none.</summary>
    public Item[] TakeBlobsToReset()
    {
        lock (_resets) {
            var reset = _resetBlobs;
            _resetBlobs = false;

            return reset ? [.. room.GetRoomItemHandler().GetFloor.Where(item => IsBlob(item) && !_blobs.Contains(item))] : [];
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

            // One more fast pass reports the state writes of the resets that were just made.
            _reported = due.Length > 0;

            return [.. due.Where(button => ReferenceEquals(room.GetRoomItemHandler().GetItem(button.Id), button))];
        }
    }

    public static string NextState(Item item)
    {
        var modes = Math.Max(2, item.Definition.Modes);

        return int.TryParse(item.LegacyDataString, out var current) && current >= 0 && current < modes - 1 ? (current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";
    }
}
