using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Interactor;

/// <summary>
/// A used vending machine runs on its own timer, not on the room tick: it hands its item over 1250 ms after the use and goes back to 0
/// at 1500 ms. A machine without a hand item goes back to 0 at 1000 ms.
/// </summary>
public sealed class VendingDispenses
{
    public const long HandItemMilliseconds = 1250;
    public const long ResetMilliseconds = 1500;
    public const long ResetWithoutHandItemMilliseconds = 1000;
    private static readonly ConditionalWeakTable<Room, VendingDispenses> Rooms = new();
    private readonly List<Dispense> _pending = [];

    public static VendingDispenses For(Room room) => Rooms.GetValue(room, _ => new());

    public bool HasPending
    {
        get
        {
            lock (_pending) {
                return _pending.Count > 0;
            }
        }
    }

    public void Start(Item machine, long nowMs)
    {
        lock (_pending) {
            _pending.RemoveAll(dispense => dispense.Machine == machine);
            _pending.Add(new(machine, nowMs, machine.Definition.VendingIds.Count > 0));
        }
    }

    /// <summary>The machines whose hand item is due, then the machines whose reset to 0 is due.</summary>
    public (Item[] HandOver, Item[] Reset) TakeDue(long nowMs)
    {
        lock (_pending) {
            var handOver = _pending.Where(dispense => dispense.HandItem && !dispense.HandedOver && nowMs >= dispense.StartMs + HandItemMilliseconds).ToArray();

            foreach (var dispense in handOver) {
                dispense.HandedOver = true;
            }

            var reset = _pending.Where(dispense => nowMs >= dispense.StartMs + (dispense.HandItem ? ResetMilliseconds : ResetWithoutHandItemMilliseconds)).ToArray();
            _pending.RemoveAll(reset.Contains);

            return ([.. handOver.Select(dispense => dispense.Machine)], [.. reset.Select(dispense => dispense.Machine)]);
        }
    }

    private sealed class Dispense(Item machine, long startMs, bool handItem)
    {
        public Item Machine { get; } = machine;
        public long StartMs { get; } = startMs;
        public bool HandItem { get; } = handItem;
        public bool HandedOver { get; set; }
    }
}
