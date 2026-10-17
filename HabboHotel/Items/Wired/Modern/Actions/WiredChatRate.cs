using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// The room's Show Message budget: at most <see cref="Limit"/> messages in any <see cref="WindowMilliseconds"/>. Observed on AIR
/// WIN63-202610151745-318700621 (r97_msg_rate, wf_act_show_message.cov_styles): ten messages from one firing go out, the next firings
/// 1.5 s and 3 s later show nothing, and a firing 7 s after the first shows all ten again; after ten messages at +1.5 s and +3.0 s,
/// firings at +4.5 s and +6.0 s showed nothing and one at +7.5 s showed its four.
/// </summary>
public sealed class WiredChatRate
{
    public const int Limit = 10;
    public const long WindowMilliseconds = 5000;
    private static readonly ConditionalWeakTable<Room, WiredChatRate> Rooms = new();
    private readonly Queue<long> _sent = new();

    public static WiredChatRate For(Room room) => Rooms.GetValue(room, _ => new());

    public bool TryAdmit(long nowMs)
    {
        lock (_sent) {
            while (_sent.Count > 0 && nowMs - _sent.Peek() >= WindowMilliseconds) {
                _sent.Dequeue();
            }

            if (_sent.Count >= Limit) {
                return false;
            }

            _sent.Enqueue(nowMs);

            return true;
        }
    }
}
