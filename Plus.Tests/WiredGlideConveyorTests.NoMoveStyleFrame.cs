using System.Buffers.Binary;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// A curve move reaches the client as one WiredMovements frame that carries the curve. No other frame announces the curve.
public sealed partial class WiredGlideConveyorTests
{
    private const uint NoStyleHeader = 5110;

    private static void AssertNoStyleFrame(IEnumerable<byte[]> packets) =>
        Assert.DoesNotContain(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == NoStyleHeader);

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ACurveMoveOfFurniAndUserSendsOnlyTheWiredMovementsFrame(bool furniBox, bool userBox)
    {
        var (f, rider, packets) = MixedMoveRoom(true, 0, userBox, furniBox, curveStrength: 80);

        FireMixed(f, rider);

        AssertNoStyleFrame(packets);
        var moves = ReadTrajectoryPublication([Assert.Single(packets.Where(IsMovementBody))]);
        Assert.Equal(new (int, int, int?)[] { (1, (int)MixedBlockId, 80), (0, rider.VirtualId, 80) }.Where(move => move.Item1 == 1 ? furniBox : userBox),
            moves.Select(move => (move.Type, move.Id, move.Strength)));
    }

    [Theory]
    [InlineData("v2")]
    [InlineData("legacy")]
    public void ACarriedRiderAndAFurniQueueWithACurveSendOnlyOneWiredMovementsFrame(string movementEngine)
    {
        var layout = FastQueueLayout(2).ToList();
        layout.Add((19, 4, 12, 4, 0, "wf_xtra_mov_curve", JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [7, 100, 100, 0, 0, 0, 0]
        })));
        layout.Add((20, 6, 12, 4, 0, "wf_xtra_mov_curve", JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [7, 100, 100, 0, 0, 0, 0]
        })));
        var f = new Fixture(layout.ToArray(), live: true, movementEngine: movementEngine);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);

        f.Advance(250);

        AssertNoStyleFrame(packets);
        var moves = ReadTrajectoryPublication([Assert.Single(packets.Where(IsMovementBody))]);
        Assert.All(moves, move => Assert.Equal(100, move.Strength));
        Assert.Equal(7, moves.Count(move => move.Type == 1));
        Assert.Equal(rider.VirtualId, Assert.Single(moves, move => move.Type == 0).Id);
    }

    [Fact]
    public void ACurveMoveOfARiderAndItsHorseSendsOnlyWiredMovementsFrames()
    {
        var f = new Fixture(FastQueueLayout(2), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var horse = f.Walker(2, 7, 9, 7, 10);
        f.Owned(() => horse.SetPos(rider.X, rider.Y, rider.Z));
        rider.RidingHorse = true;
        rider.HorseId = horse.VirtualId;
        horse.RidingHorse = true;
        horse.HorseId = rider.VirtualId;
        var context = f.MovementContext();
        context.Policy.Addons.Curve = new(7, 100, 100);
        var packets = CapturePublication(rider);
        var movement = new WiredRoomMovement((_, _, _) => { });

        f.Owned(() => Assert.True(movement.MoveAvatar(context, rider, 4, 10, true)));

        Assert.Equal((4, 10), (horse.X, horse.Y));
        AssertNoStyleFrame(packets);
        var moves = ReadTrajectoryPublication(packets);
        Assert.Equal(new[] { rider.VirtualId, horse.VirtualId }.Order(), moves.Select(move => move.Id).Order());
        Assert.All(moves, move => Assert.Equal((0, 100), (move.Type, move.Strength)));
    }
}
