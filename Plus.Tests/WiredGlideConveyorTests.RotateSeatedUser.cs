using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A clockwise Move and Rotate User on a user who sits on a chair or lies on a bed still sends the turned direction in
    // WiredMovements, but the user keeps the furni direction: the next UserUpdate shows the old head and body direction, and the
    // next turn starts from it again.
    [Theory]
    [InlineData("chair", "sit")]
    [InlineData("bed", "lay")]
    public void RotateUserKeepsTheDirectionOfAUserOnASeatOrBed(string furni, string posture)
    {
        var (f, rider, packets) = RotateUserOnFurni(furni);
        Assert.True(rider.Statusses.ContainsKey(posture));
        Assert.Equal((2, 2), (rider.RotBody, rider.RotHead));

        for (var firing = 0; firing < 2; firing++) {
            var mark = packets.Count;
            FireRotateUser(f, rider);
            f.Advance(500);
            var sent = packets.Skip(mark).ToArray();

            Assert.Equal([new MovementEntry(3, rider.VirtualId, 3, 3)], ReadMovementEntries(sent).Where(entry => entry.Type == 3));
            var update = Assert.Single(ReadUserUpdates(sent), user => user.Index == rider.VirtualId);
            Assert.Equal((2, 2), (update.Head, update.Body));
            Assert.Contains("/" + posture + " ", update.Status);
        }
    }

    [Fact]
    public void RotateUserTurnsAUserWhoSitsOnTheFloor()
    {
        var (f, rider, packets) = RotateUserRoom(9, 2);
        f.Owned(() =>
        {
            rider.Statusses.TryAdd("sit", "0.55 1");
            rider.IsSitting = true;
            rider.UpdateNeeded = true;
        });
        f.Advance(500);
        var mark = packets.Count;

        FireRotateUser(f, rider);
        f.Advance(500);
        var sent = packets.Skip(mark).ToArray();

        Assert.Equal([new MovementEntry(3, rider.VirtualId, 3, 3)], ReadMovementEntries(sent).Where(entry => entry.Type == 3));
        var update = Assert.Single(ReadUserUpdates(sent), user => user.Index == rider.VirtualId);
        Assert.Equal((3, 3), (update.Head, update.Body));
    }

    private static (Fixture Fixture, RoomUser Rider, List<byte[]> Packets) RotateUserOnFurni(string furni)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [-1, 9],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_move_rotate_user", "native:" + JsonSerializer.Serialize(native)),
            (30, 4, 11, 0.0, 2, furni, null)
        };
        var f = new Fixture(layout, live: true);
        var seat = f.Items[30];
        seat.Definition.IsSeat = furni == "chair";
        seat.Definition.Walkable = false;
        seat.Definition.Stackable = false;
        f.Owned(() =>
        {
            f.Room.GetGameMap().Navigation!.Inputs.Attach(seat);
            f.Room.GetGameMap().GenerateMaps();
        });
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Owned(() => PostureService.Apply(f.Room, f.Room.GetGameMap().Navigation!.Grid, rider));
        f.Advance(500);
        Assert.Equal((4, 11), (rider.X, rider.Y));

        return (f, rider, CapturePublication(rider));
    }

    private sealed record UserUpdateEntry(int Index, int Head, int Body, string Status);

    // UserUpdate grammar: count; per user index, x, y, z string, head, body, third i32, status string.
    private static List<UserUpdateEntry> ReadUserUpdates(IEnumerable<byte[]> packets)
    {
        var entries = new List<UserUpdateEntry>();

        foreach (var bytes in packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.UserUpdateComposer)) {
            var position = 6;
            int Int()
            {
                var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
                position += 4;

                return value;
            }
            string String()
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                var value = Encoding.UTF8.GetString(bytes, position + 2, length);
                position += 2 + length;

                return value;
            }
            var count = Int();

            for (var index = 0; index < count; index++) {
                var id = Int();
                _ = Int();
                _ = Int();
                _ = String();
                var head = Int();
                var body = Int();
                _ = Int();
                entries.Add(new(id, head, body, String()));
            }

            Assert.Equal(bytes.Length, position);
        }

        return entries;
    }
}
