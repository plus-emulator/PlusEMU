using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed class UserUpdateComposerGrammarTests
{
    private static readonly Dictionary<string, RoomUserStatusSnapshot[]> Cases = new()
    {
        ["empty"] = [],
        ["stationary"] = [new(5, 9, 9, "0", 3, 3, "/flatctrl 4//")],
        ["moving"] = [new(5, 8, 9, "0.5", 2, 2, "/flatctrl 4/mv 9,9,0//")],
        ["wf"] = [new(5, 9, 8, "1", 3, 3, "/flatctrl 4/wf//")],
        ["two humans"] = [new(5, 9, 9, "0", 3, 3, "/flatctrl 4//"), new(6, 10, 2, "1.25", 6, 6, "/sit 0.5//")],
        ["unicode status"] = [new(7, 1, 1, "0", 0, 0, "/sign \u00e9\u4e2d//")]
    };

    [Theory]
    [InlineData("empty")]
    [InlineData("stationary")]
    [InlineData("moving")]
    [InlineData("wf")]
    [InlineData("two humans")]
    [InlineData("unicode status")]
    public void UserUpdateIsReadByTheSupportedOctoberUserUpdateGrammarToTheExactEnd(string name)
    {
        var users = Cases[name];
        Assert.Equal(3744u, ServerPacketHeader.UserUpdateComposer);
        var bytes = TypedFurnitureStateTests.Payload(new UserUpdateComposer([.. users]));

        var read = ReadUserUpdate(bytes);

        Assert.Equal(users.Select(user => (user.VirtualId, user.X, user.Y, user.Z, user.HeadRotation, user.BodyRotation, 0, user.Status)),
            read.Select(user => (user.Index, user.X, user.Y, user.Z, user.Head, user.Body, user.Third, user.Status)));
    }

    private sealed record ReadUser(int Index, int X, int Y, string Z, int Head, int Body, int Third, string Status);

    // i32 count; per user index, x, y, z string, head, body, third, status string. Anything short or left over is a failure.
    private static List<ReadUser> ReadUserUpdate(byte[] bytes)
    {
        var position = 0;
        int Int()
        {
            Assert.True(position + 4 <= bytes.Length, $"i32 at {position} runs past the {bytes.Length}-byte payload");
            var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
            position += 4;

            return value;
        }
        string String()
        {
            Assert.True(position + 2 <= bytes.Length, $"string length at {position} runs past the {bytes.Length}-byte payload");
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
            Assert.True(position + 2 + length <= bytes.Length, $"string of {length} bytes at {position} runs past the {bytes.Length}-byte payload");
            var value = Encoding.UTF8.GetString(bytes, position + 2, length);
            position += 2 + length;

            return value;
        }

        var users = new List<ReadUser>();
        var count = Int();

        for (var i = 0; i < count; i++) {
            users.Add(new(Int(), Int(), Int(), String(), Int(), Int(), Int(), String()));
        }

        Assert.Equal(bytes.Length, position);

        return users;
    }
}
