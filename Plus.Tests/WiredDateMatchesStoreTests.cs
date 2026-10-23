using Xunit;

namespace Plus.Tests;

public class WiredDateMatchesStoreTests
{
    // An inverted year range reads back with its end raised to its start; an inverted day range is kept.
    [Fact]
    public void DateMatchesRaisesAnInvertedYearRangeEnd()
    {
        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredBoxRegistry.TryGet("wf_cnd_match_date", out var descriptor));
        var native = Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [1, 1, 127, 20, 10, 4095, 9999, 0]
        };
        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime));
        Assert.Equal(new[] { 1, 1, 127, 20, 10, 4095, 9999, 9999 }, runtime.Origin!.Native.OwnedIntParams.ToArray());
    }
}
