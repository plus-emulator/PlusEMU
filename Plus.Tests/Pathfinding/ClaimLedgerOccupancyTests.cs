using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests.Pathfinding;

public class ClaimLedgerOccupancyTests
{
    [Fact]
    public void DirectOccupancyMatchesEverySlotAndExcludedGroupAcrossMutations()
    {
        var ledger = new ClaimLedger(4, 4);
        var first = Actor();
        var second = Actor();
        ledger.Move(first, 1, 1, false, 10);
        ledger.Move(second, null, 2, false, 20);
        AssertViews(ledger);
        ledger.TryClaim(first, 3, ClaimKind.Exclusive, TargetOccupancy.None);
        ledger.TryClaim(second, 3, ClaimKind.Roller, TargetOccupancy.None);
        AssertViews(ledger);
        ledger.Move(first, 2, 2, true, 10);
        AssertViews(ledger);
        ledger.ReleaseBatch(first);
        AssertViews(ledger);
        ledger.ReleaseRollers();
        ledger.Remove(second);
        AssertViews(ledger);
        Assert.Equal(TargetOccupancy.Walking, ledger.OccupancyAt(2, 0));
        Assert.Equal(TargetOccupancy.None, ledger.OccupancyAt(2, 10));
    }

    [Fact]
    public void DirectOccupancyDoesNotAllocateForRepeatedExecutionChecks()
    {
        var ledger = new ClaimLedger(4, 4);
        var first = Actor();
        var second = Actor();
        ledger.Move(first, 1, 1, true, 10);
        ledger.Move(second, null, 1, false, 20);
        ledger.TryClaim(first, 1, ClaimKind.Exclusive, TargetOccupancy.None);
        ledger.TryClaim(second, 1, ClaimKind.Roller, TargetOccupancy.None);

        for (var warm = 0; warm < 100; warm++) {
            ledger.OccupancyAt(1, 10);
        }

        // Tiered JIT promotion can land inside a window on a loaded host and charge runtime bookkeeping
        // to this thread. A real per-call allocation shows up in every window, so the quietest one counts.
        var result = TargetOccupancy.None;
        var allocated = long.MaxValue;

        for (var window = 0; window < 5 && allocated != 0; window++) {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var query = 0; query < 1000; query++) {
                result |= ledger.OccupancyAt(1, 10);
            }

            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(TargetOccupancy.OffGraph | TargetOccupancy.RollerClaim, result);
        Assert.Equal(0, allocated);
    }

    private static void AssertViews(ClaimLedger ledger)
    {
        foreach (var group in new long[] { 0, 10, 20 }) {
            var snapshot = ledger.Snapshot(group);

            for (var slot = 0; slot < snapshot.Targets.Length; slot++) {
                Assert.Equal(snapshot.Targets[slot], ledger.OccupancyAt(slot, group));
            }
        }
    }

    private static RoomUser Actor() => (RoomUser)RuntimeHelpers.GetUninitializedObject(typeof(RoomUser));
}
