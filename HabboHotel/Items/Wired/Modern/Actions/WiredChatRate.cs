using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// The room's Show Message budget: at most <see cref="Limit"/> messages in any <see cref="WindowMilliseconds"/>,
/// and at most <see cref="LongLimit"/> in any <see cref="LongWindowMilliseconds"/>. <see cref="TryAdmit"/> refuses a message over the budget.
/// </summary>
public sealed class WiredChatRate
{
    public const int Limit = 10;
    public const long WindowMilliseconds = 5000;
    public const int LongLimit = 40;
    public const long LongWindowMilliseconds = 30000;
    private static readonly ConditionalWeakTable<Room, WiredChatRate> Rooms = new();
    private readonly Queue<long> _sent = new();

    public static WiredChatRate For(Room room) => Rooms.GetValue(room, _ => new());

    public bool TryAdmit(long nowMs)
    {
        lock (_sent) {
            while (_sent.Count > 0 && nowMs - _sent.Peek() >= LongWindowMilliseconds) {
                _sent.Dequeue();
            }

            if (_sent.Count >= LongLimit || _sent.Count >= Limit && nowMs - _sent.ElementAt(_sent.Count - Limit) < WindowMilliseconds) {
                return false;
            }

            _sent.Enqueue(nowMs);

            return true;
        }
    }
}
