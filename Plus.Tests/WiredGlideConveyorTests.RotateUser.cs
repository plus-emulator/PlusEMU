using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Official native 42, one standing human, user source 0, rotation only: clockwise (9) turns one step, counter-clockwise (10) one step back.
    [Theory]
    [InlineData(9, 7, 0)]
    [InlineData(9, 0, 1)]
    [InlineData(9, 1, 2)]
    [InlineData(10, 2, 1)]
    [InlineData(10, 1, 0)]
    [InlineData(10, 0, 7)]
    public void NativeCompiledRotateUserTurnsAStandingHumanOneStepForClockwiseAndCounterClockwise(int raw, int start, int expected)
    {
        var (f, rider, packets) = RotateUserRoom(raw, start);

        FireRotateUser(f, rider);

        Assert.Equal((expected, expected), (rider.RotBody, rider.RotHead));
        Assert.Equal([new MovementEntry(3, rider.VirtualId, expected, expected)], ReadMovementEntries(packets).Where(entry => entry.Type == 3));
    }

    // Absolute rotation publishes an avatar direction entry, including when the direction is already the requested one.
    [Theory]
    [InlineData(3, 0)]
    [InlineData(0, 0)]
    [InlineData(6, 7)]
    [InlineData(7, 7)]
    public void NativeCompiledAbsoluteRotateUserPublishesOneDirectionEntryForTheStandingHuman(int start, int target)
    {
        var (f, rider, packets) = RotateUserRoom(target, start);

        FireRotateUser(f, rider);

        var directions = ReadMovementEntries(packets).Where(entry => entry.Type == 3).ToArray();
        Assert.Equal([new MovementEntry(3, rider.VirtualId, target, target)], directions);
        Assert.Equal((target, target), (rider.RotBody, rider.RotHead));
        Assert.Equal((4, 11), (rider.X, rider.Y));
    }

    // Control: the final direction of a changed absolute rotation already matches the official cells.
    [Theory]
    [InlineData(3, 0)]
    [InlineData(6, 7)]
    public void NativeCompiledAbsoluteRotateUserTurnsAChangedDirection(int start, int target)
    {
        var (f, rider, _) = RotateUserRoom(target, start);

        FireRotateUser(f, rider);

        Assert.Equal((target, target), (rider.RotBody, rider.RotHead));
    }

    private static (Fixture Fixture, RoomUser Rider, List<byte[]> Packets) RotateUserRoom(int raw, int start)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        // The saved row is the native editor shape; the real store compiles it, so a stored 8/9 comes from native 9/10 only through that compile.
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [-1, raw],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_move_rotate_user", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var room = f.MovementContext().Room;

        // The publication path needs a registered avatar with a client in a mapped room.
        Assert.Same(rider, room.GetRoomUserManager().GetRoomUserByVirtualId(rider.VirtualId));
        Assert.NotNull(rider.GetClient());
        Assert.Contains(rider, room.GetGameMap().GetRoomUsers(new(rider.X, rider.Y)));
        var stored = ((IWiredConfiguredItem)f.Box(22)).Configuration;
        Assert.NotNull(stored.Origin?.Native);
        Assert.Equal([-1, raw is 9 ? 8 : raw is 10 ? 9 : raw, 0], stored.IntParams.ToArray());

        // Standing: the walk to the start tile is finished and nothing is queued.
        Assert.Equal((4, 11), (rider.X, rider.Y));
        Assert.False(rider.IsWalking);
        rider.RotBody = start;
        rider.RotHead = start;

        return (f, rider, CapturePublication(rider));
    }

    private static void FireRotateUser(Fixture f, RoomUser rider)
    {
        f.Owned(() => rider.OnChat(0, "pulse", false));
        f.Advance(200);
    }

    private sealed record MovementEntry(int Type, int Id, int BodyRotation, int HeadRotation);

    // Strict WiredMovements reader: type 0 and 1 are consumed whole, type 3 is id, body, head.
    private static List<MovementEntry> ReadMovementEntries(IEnumerable<byte[]> packets)
    {
        var entries = new List<MovementEntry>();

        foreach (var bytes in packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer)) {
            var position = 6;
            int Int()
            {
                var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
                position += 4;

                return value;
            }
            void String() => position += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
            var count = Int();

            for (var index = 0; index < count; index++) {
                var type = Int();

                if (type == 3) {
                    var id = Int();
                    var body = Int();
                    entries.Add(new(3, id, body, Int()));

                    continue;
                }

                Assert.InRange(type, 0, 1);
                _ = Int();
                _ = Int();
                _ = Int();
                _ = Int();
                String();
                String();
                var movedId = Int();

                if (type == 0) {
                    _ = Int();
                }

                _ = Int();
                var rotation = Int();
                var head = type == 0 ? Int() : rotation;

                if (type == 0) {
                    Assert.Equal(0, bytes[position++]);
                }
                else {
                    Assert.Equal(0, bytes[position++]);
                    Assert.Equal(0, bytes[position++]);
                }

                entries.Add(new(type, movedId, rotation, head));
            }

            Assert.Equal(bytes.Length, position);
        }

        return entries;
    }
}
