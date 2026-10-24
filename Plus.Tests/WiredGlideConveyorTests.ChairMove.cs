using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private static void SeatFurni(Fixture f, uint id)
    {
        var seat = f.Items[id];
        seat.Definition.IsSeat = true;
        seat.Definition.Walkable = false;
        seat.Definition.Stackable = false;
        f.Owned(() =>
        {
            f.Room.GetGameMap().Navigation!.Inputs.Attach(seat);
            f.Room.GetGameMap().GenerateMaps();
        });
    }

    // A chair on a stack of two blocks moves one furni at a time in a random order. A block that comes after the chair cannot land on it and is left out.
    [Fact]
    public void MoveRotateMovesAChairOnAStackOneFurniAtATime()
    {
        var heights = new SortedSet<double>();

        for (var firing = 0; firing < 36; firing++) {
            var f = new Fixture(
            [
                (80, 6, 12, 0.0, 0, "bc_tile_1", null),
                (81, 6, 12, 1.0, 0, "bc_tile_1", null),
                (82, 6, 12, 2.0, 2, "chair", null),
                (90, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
                (91, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(9, 80, 81, 82))
            ], live: true);
            SeatFurni(f, 82);
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);
            var packets = CapturePublication(rider);
            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            var batch = Assert.Single(ReadMovementPackets(packets));
            var moved = new uint[] { 80, 81, 82 }.Where(id => f.Items[id].GetX == 7).ToArray();
            Assert.Equal(moved.Order(), batch.Select(entry => (uint)entry.Id).Order());
            Assert.Contains(82u, moved);
            Assert.All(moved.Where(id => id != 82), id => Assert.True(f.Items[id].GetZ < f.Items[82].GetZ));
            heights.Add(f.Items[82].GetZ);
        }

        Assert.Equal([0.0, 1.0, 2.0], heights);
    }

    // Turning a chair stack in place keeps the blocks under the chair.
    [Fact]
    public void MoveRotateTurnsAChairStackInPlaceWithoutPuttingABlockOnTheChair()
    {
        for (var firing = 0; firing < 24; firing++) {
            var f = new Fixture(
            [
                (80, 6, 12, 0.0, 0, "bc_tile_1", null),
                (81, 6, 12, 1.0, 0, "bc_tile_1", null),
                (82, 6, 12, 2.0, 2, "chair", null),
                (90, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
                (91, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateWithTurnConfig(0, 1, 80, 81, 82))
            ], live: true);
            SeatFurni(f, 82);
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);
            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            Assert.Equal([0.0, 1.0, 2.0], new uint[] { 80, 81, 82 }.Select(id => f.Items[id].GetZ));
            Assert.All(new uint[] { 80, 81, 82 }, id => Assert.Equal((6, 12), (f.Items[id].GetX, f.Items[id].GetY)));
        }
    }

    // Three Move boxes bring an empty chair and two blocks onto one tile: one WiredMovements.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MoveBoxesWithAnEmptyChairShareOneWiredMovements(bool chairFirst)
    {
        for (var firing = 0; firing < 12; firing++) {
            var boxes = new List<string> { MoveRotateConfig(6, 82), MoveRotateConfig(7, 80), MoveRotateConfig(5, 81) };

            if (!chairFirst) {
                boxes.Reverse();
            }

            var f = new Fixture(
            [
                (80, 10, 10, 0.0, 0, "bc_tile_1", null),
                (81, 8, 10, 0.0, 0, "bc_tile_1", null),
                (82, 9, 9, 0.0, 2, "chair", null),
                (90, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
                (91, 4, 14, 0.65, 0, "wf_act_move_rotate", boxes[0]),
                (92, 4, 14, 1.3, 0, "wf_act_move_rotate", boxes[1]),
                (93, 4, 14, 1.95, 0, "wf_act_move_rotate", boxes[2])
            ], live: true);
            SeatFurni(f, 82);
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);
            var packets = CapturePublication(rider);
            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            Assert.Single(ReadMovementPackets(packets));
        }
    }

    private static (Fixture, RoomUser, List<byte[]>) Seated(string second)
    {
        var f = new Fixture(
        [
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", SaysConfig("go")),
            (22, 4, 12, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(9, 30)),
            (23, 3, 12, 0.0, 0, "wf_trg_says_something", SaysConfig("back")),
            (24, 3, 12, 0.65, 0, "wf_act_move_rotate", second),
            (30, 6, 6, 0.0, 2, "chair", null)
        ], live: true);
        SeatFurni(f, 30);
        var rider = f.Walker(1, 6, 5, 6, 6);
        f.Owned(() => PostureService.Apply(f.Room, f.Room.GetGameMap().Navigation!.Grid, rider));
        f.Advance(500);
        Assert.True(rider.Statusses.ContainsKey("sit"));

        return (f, rider, CapturePublication(rider));
    }

    // The sitter stands up on the old tile on the next room tick, and the chair cannot move back onto the standing user.
    [Fact]
    public void AChairCannotMoveOntoTheUserItLeftStanding()
    {
        var (f, rider, packets) = Seated(MoveRotateConfig(11, 30));

        f.Owned(() => rider.OnChat(0, "go", false));
        f.Advance(500);
        Assert.Equal((7, 7), (f.Items[30].GetX, f.Items[30].GetY));
        Assert.Equal((6, 6), (rider.X, rider.Y));
        Assert.False(rider.Statusses.ContainsKey("sit"));

        var mark = packets.Count;
        f.Owned(() => rider.OnChat(0, "back", false));
        f.Advance(500);
        Assert.Equal((7, 7), (f.Items[30].GetX, f.Items[30].GetY));
        Assert.Empty(ReadMovementPackets(packets.Skip(mark)));
    }

    // A chair turned under a sitter: an ObjectUpdate and no WiredMovements; on the next room tick the sitter takes the new direction and stays seated.
    [Fact]
    public void TurningAChairUnderASitterTurnsHimAndKeepsHimSeated()
    {
        var (f, rider, packets) = Seated(MoveRotateWithTurnConfig(0, 1, 30));

        foreach (var expected in new[] { 4, 6, 0, 2 }) {
            var mark = packets.Count;
            f.Owned(() => rider.OnChat(0, "back", false));
            f.Advance(500);
            var sent = packets.Skip(mark).ToArray();

            Assert.Empty(ReadMovementPackets(sent));
            Assert.Contains(sent, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.ObjectUpdateComposer);
            var update = Assert.Single(ReadUserUpdates(sent), user => user.Index == rider.VirtualId);
            Assert.Equal((expected, expected), (update.Head, update.Body));
            Assert.Contains("/sit ", update.Status);
        }
    }
}
