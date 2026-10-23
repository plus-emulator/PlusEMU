using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// The room's Show Message budget: at most <see cref="Limit"/> messages in any <see cref="WindowMilliseconds"/>.
/// A second budget allows <see cref="LongLimit"/> messages in any <see cref="LongWindowMilliseconds"/>.
/// A firing on the edge of a window gets part of a batch.
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
