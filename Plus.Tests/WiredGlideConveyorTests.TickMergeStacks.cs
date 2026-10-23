using System.Buffers.Binary;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Two stacks that fire on the same say each move their own block. The tick sends one ObjectsDataUpdate with the flashes of
    // both stacks, then one WiredMovements that lists A before B on every say, whichever stack ran first.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoStacksFiringInOneTickShareOneFlashBatchAndOneWiredMovements(bool secondStackFirst)
    {
        var f = new Fixture(
        [
            (80, 6, 12, 0.0, 0, "bc_tile_1", null),
            (81, 9, 12, 0.0, 0, "bc_tile_1", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("tsk")),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 80)),
            (85, 5, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("tsk")),
            (86, 5, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(7, 81))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.SetEngineCallback("_effectOrderRandom", new StackOrderRandom(secondStackFirst));
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "tsk", false));
        f.Advance(200);

        var frames = packets.Select(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes))
            .Where(header => header is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.WiredMovementsComposer).ToArray();
        Assert.Equal([ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.WiredMovementsComposer], frames);
        var flash = packets.Single(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Superset(new HashSet<uint> { 83, 84, 85, 86 },
            ReadBatch((ServerPacketHeader.ObjectsDataUpdateComposer, flash[6..])).Select(entry => entry.Id).ToHashSet());
        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(80, 6, 7), (81, 9, 8)], batch.Select(entry => (entry.Id, entry.FromX, entry.ToX)));
    }

    // On a live server the clock moves on while the first stack runs, so the second stack of the same say fires a few ms later.
    // Both stacks still share one WiredMovements.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoStacksOfOneSayShareOneWiredMovementsWhileTheClockMoves(bool secondStackFirst)
    {
        var f = new Fixture(
        [
            (80, 6, 12, 0.0, 0, "bc_tile_1", null),
            (81, 9, 12, 0.0, 0, "bc_tile_1", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("tsk")),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 80)),
            (85, 5, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("tsk")),
            (86, 5, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(7, 81))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.SetEngineCallback("_effectOrderRandom", new StackOrderRandom(secondStackFirst));
        f.Advance(1000);
        var packets = CapturePublication(rider);
        var clock = (Func<long>)f.EngineField("_now")!;
        var now = clock();
        f.SetEngineCallback("_now", (Func<long>)(() => now++));

        f.Owned(() => rider.OnChat(0, "tsk", false));
        f.SetEngineCallback("_now", clock);
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(80, 6, 7), (81, 9, 8)], batch.Select(entry => (entry.Id, entry.FromX, entry.ToX)));
    }

    // A walk onto furni queues its stacks for the room's next pass, which runs them at one time, so they share one WiredMovements
    // even while the clock moves.
    [Fact]
    public void TwoStacksOfOneWalkOnShareOneWiredMovementsWhileTheClockMoves()
    {
        var walkOn = JsonSerializer.Serialize(new WiredConfiguration { IntParams = [100], SelectedItems = [87] });
        var f = new Fixture(
        [
            (80, 6, 12, 0.0, 0, "bc_tile_1", null),
            (81, 9, 12, 0.0, 0, "bc_tile_1", null),
            (87, 4, 12, 0.0, 0, "wf_colortile", null),
            (83, 4, 14, 0.0, 0, "wf_trg_walks_on_furni", walkOn),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 80)),
            (85, 5, 14, 0.0, 0, "wf_trg_walks_on_furni", walkOn),
            (86, 5, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(7, 81))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);
        var clock = (Func<long>)f.EngineField("_now")!;
        var ticking = false;
        var drift = 0L;
        f.SetEngineCallback("_now", (Func<long>)(() => clock() + (ticking ? ++drift : drift)));

        rider.MoveTo(4, 12);
        ticking = true;
        f.Advance(1000);

        Assert.Equal((4, 12), (rider.X, rider.Y));
        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(80, 6, 7), (81, 9, 8)], batch.Select(entry => (entry.Id, entry.FromX, entry.ToX)));
    }

    // Keeps the stacks of one event in their listed order, or swaps them.
    private sealed class StackOrderRandom(bool swap) : Random
    {
        public override int Next(int minValue, int maxValue) => swap ? maxValue - 1 : minValue;
        public override int Next(int maxValue) => maxValue - 1;
    }
}
