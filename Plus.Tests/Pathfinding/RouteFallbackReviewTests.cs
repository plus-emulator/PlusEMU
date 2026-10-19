using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// A temporary claim must not erase the walk before the next hard obstruction.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void AReleasedRollerClaimAllowsProgressUpToTheHardBlockage()
    {
        FallbackModel(FallbackLongDeadEnd);
        var gate = FallbackGate(20, 6, 1);
        var actor = FallbackActor(0, 1, new());
        var owner = FallbackBot(3, 3, 2);
        ExecutorTick();
        actor.MoveTo(7, 1);
        ExecutorTick();
        FallbackSetGate(gate, open: false);
        ExecutorTick();
        Assert.True(actor.Movement.HasIntent);
        // A roller claim lasts one user phase; the next step must resume afterwards.
        Assert.True(_room.GetGameMap().Navigation!.Executor.Claims.TryClaim(owner, 1 * 8 + 2, ClaimKind.Roller, TargetOccupancy.None));
        FallbackRun(actor, 20);
        FallbackAssertStopped(actor, 5, 1);
    }
}
