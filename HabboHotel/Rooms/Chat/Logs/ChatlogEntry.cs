using System.Text;
using System.Text.Unicode;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Logs;

public sealed class ChatlogEntry
{
    private readonly WeakReference<Habbo>? _playerReference;
    private readonly WeakReference<Room>? _roomReference;

    public ChatlogEntry(int playerId, uint roomId, string message, DateTimeOffset createdAt, Habbo? player = null, Room? instance = null)
    {
        PlayerId = playerId;
        RoomId = roomId;
        Message = FitColumn(message);
        CreatedAt = createdAt.ToUniversalTime();

        if (player != null) {
            _playerReference = new(player);
        }

        if (instance != null) {
            _roomReference = new(instance);
        }
    }

    public int PlayerId { get; }

    public uint RoomId { get; }

    public string Message { get; }

    public DateTimeOffset CreatedAt { get; }

    public Habbo? PlayerNullable() => _playerReference != null && _playerReference.TryGetTarget(out var player) ? player : null;

    public Room? RoomNullable() => _roomReference != null && _roomReference.TryGetTarget(out var room) && !room.MDisposed ? room : null;

    // `chatlogs.message` is a `text` column, which holds 65535 bytes of UTF-8.
    private static string FitColumn(string message)
    {
        const int maxBytes = ushort.MaxValue;

        if (Encoding.UTF8.GetByteCount(message) <= maxBytes) {
            return message;
        }

        Utf8.FromUtf16(message, new byte[maxBytes], out var kept, out _);

        return message[..kept];
    }
}
