using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // Observed on AIR WIN63-202610201032-107964540 (a counter stopped at 5:30.5): comparison 3 is stored and passes when the counter is
    // less than or equal to the value.
    [Theory]
    [InlineData(1, 5, 30, 1, 3, true)]
    [InlineData(1, 5, 31, 0, 3, true)]
    [InlineData(1, 6, 0, 0, 3, true)]
    [InlineData(1, 5, 30, 0, 3, false)]
    [InlineData(1, 5, 30, 1, 1, true)]
    [InlineData(1, 5, 30, 0, 2, true)]
    [InlineData(1, 5, 31, 0, 0, true)]
    public void CounterTimeComparisonThreeMeansLessThanOrEqual(int unused, int minutes, int seconds, int half, int comparison, bool expected)
    {
        Assert.Equal(1, unused);
        var (room, _, items) = World();
        var counter = Floor(7, "wf_upcounter1", 0, 0);
        items[counter.Id] = counter;
        var box = Box(room, "wf_cnd_counter_time_matches", item => item.Id == counter.Id ? 5 * 60_000L + 30_500L : null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [seconds, minutes, half, comparison],
            PrimaryItems = [new(counter.Id, false)]
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);

        Assert.Equal(expected, box.Execute(Context(room, [counter], [])));
    }

    // Observed on AIR WIN63-202610201032-107964540 (a block at 1.50 and one on the floor): comparison 3 is stored and passes when the
    // altitude is at most the value.
    [Theory]
    [InlineData(100, 3, 0.0, true)]
    [InlineData(0, 3, 0.0, true)]
    [InlineData(100, 3, 1.5, false)]
    [InlineData(200, 3, 1.5, true)]
    [InlineData(150, 3, 1.5, true)]
    [InlineData(150, 1, 1.5, true)]
    [InlineData(149, 1, 1.5, false)]
    [InlineData(200, 2, 1.5, false)]
    [InlineData(200, 0, 1.5, true)]
    public void AltitudeComparisonThreeMeansAtMost(int altitude, int comparison, double height, bool expected)
    {
        var (room, _, _) = World();
        var block = Floor(7, "block", 0, 0);
        block.SetState(0, 0, height);
        var box = Box(room, "wf_cnd_has_altitude", _ => null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [altitude, comparison],
            PrimaryItems = [new(block.Id, false)]
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);

        Assert.Equal(expected, box.Execute(Context(room, [block], [])));
    }
}
