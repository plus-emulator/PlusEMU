using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Fact]
    public void RepeatedQueuePulsesKeepPlacementProjectionAndFurnitureStable()
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var map = f.MovementContext().Room.GetGameMap();
        f.Advance(250);
        var expected = map.PlacementHeightMap();
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var allocation = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 200; i++) {
            f.Advance(200);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation;
        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        Console.WriteLine($"Queue 200 pulses: {elapsed:F1} ms; {allocated} bytes allocated");
        // Full-room furniture resolution on every tile move used over 860 MB here.
        // Keep ample headroom above the incremental projection's ~105 MB; do not assert wall time.
        Assert.True(allocated < 200_000_000, $"Excessive queue allocation: {allocated} bytes");
        Assert.Equal(11, rider.X);
        Assert.Equal(expected.Cast<short>(), map.PlacementHeightMap().Cast<short>());
        Assert.Equal(Enumerable.Range(4, 8), Enumerable.Range(1, 8).Select(id => f.Items[(uint)id].GetX));
        Assert.Equal(0, f.Engine.ReadStats().Pending);
    }
}
