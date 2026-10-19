using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public class NullableLookupTests
{
    [Fact]
    public void StoredChatCanBeReadWithoutLiveRoomOrPlayer()
    {
        var entry = new ChatlogEntry(12, 34, "hello", DateTimeOffset.UnixEpoch.AddSeconds(123));

        Assert.Null(entry.PlayerNullable());
        Assert.Null(entry.RoomNullable());
        Assert.Equal("hello", entry.Message);
    }
}
