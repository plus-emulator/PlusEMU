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
    // A push (+y) on a user stepping (5,3) -> (6,3) sends one walk entry (5,3) -> (6,4) in the same tick, and the user then walks
    // on to its goal.
    [Fact]
    public void PushOnAWalkingUserReplacesTheStepAndTheWalkResumes()
    {
        var (f, rider, packets) = PushRoom(4);

        PushWhileStepping(f, rider, 10);

        Assert.Equal([(5, 3, 6, 4)], ReadAvatarMoves(packets, rider.VirtualId));
        Assert.Equal((6, 4), (rider.X, rider.Y));

        f.Advance(5000);
        Assert.Equal((10, 3), (rider.X, rider.Y));
        Assert.False(rider.IsWalking);
    }

    [Fact]
    public void PushOnTheLastStepEndsTheWalkThere()
    {
        var (f, rider, packets) = PushRoom(4);

        PushWhileStepping(f, rider, 6);
        f.Advance(3000);

        Assert.Equal([(5, 3, 6, 4)], ReadAvatarMoves(packets, rider.VirtualId));
        Assert.Equal((6, 4), (rider.X, rider.Y));
        Assert.False(rider.IsWalking);
    }

    // Pushed back the way it came, the entry is (5,3) -> (5,3) every step and the walk never completes.
    [Fact]
    public void PushBackOntoTheStartTileTreadmills()
    {
        var (f, rider, packets) = PushRoom(6);

        PushWhileStepping(f, rider, 10);
        f.Advance(500);
        f.Owned(() => rider.OnChat(0, "push", false));
        f.Advance(500);
        f.Owned(() => rider.OnChat(0, "push", false));

        var moves = ReadAvatarMoves(packets, rider.VirtualId);
        Assert.Equal(3, moves.Count);
        Assert.All(moves, move => Assert.Equal((5, 3, 5, 3), move));
        Assert.Equal((5, 3), (rider.X, rider.Y));
    }

    private static (Fixture Fixture, RoomUser Rider, List<byte[]> Packets) PushRoom(int movement)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, -1],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (31, 11, 1, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "push" })),
            (32, 11, 1, 0.65, 0, "wf_act_move_rotate_user", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 2, 4, 3);
        Assert.Equal((4, 3), (rider.X, rider.Y));
        Assert.False(rider.IsWalking);

        return (f, rider, CapturePublication(rider));
    }

    // Walks towards the goal and pushes once the user is stepping (5,3) -> (6,3).
    private static void PushWhileStepping(Fixture f, RoomUser rider, int goalX)
    {
        f.Owned(() => rider.MoveTo(goalX, 3));

        for (var i = 0; i < 40 && !(rider.X == 5 && rider.SetStep && (rider.SetX, rider.SetY) == (6, 3)); i++) {
            f.Advance(50);
        }

        Assert.Equal((5, 3, 6, 3), (rider.X, rider.Y, rider.SetX, rider.SetY));
        f.Owned(() => rider.OnChat(0, "push", false));
    }

    // The avatar walk entries (type 0) of WiredMovements: from x, from y, to x, to y.
    private static List<(int FromX, int FromY, int ToX, int ToY)> ReadAvatarMoves(IEnumerable<byte[]> packets, int virtualId)
    {
        var moves = new List<(int, int, int, int)>();

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
                    position += 12;

                    continue;
                }

                var fromX = Int();
                var fromY = Int();
                var toX = Int();
                var toY = Int();
                String();
                String();
                var id = Int();

                if (type == 0) {
                    _ = Int();
                }

                _ = Int();
                _ = Int();

                if (type == 0) {
                    _ = Int();
                    position++;
                }
                else {
                    position += 2;
                }

                if (type == 0 && id == virtualId) {
                    moves.Add((fromX, fromY, toX, toY));
                }
            }
        }

        return moves;
    }
}
