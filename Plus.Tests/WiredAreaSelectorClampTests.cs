using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>The area selectors clamp each area int to 0..64 when saved.</summary>
public sealed class WiredAreaSelectorClampTests
{
    [Theory]
    [InlineData("wf_slc_users_area", new[] { -5, -5, 3, 3 }, new[] { 0, 0, 3, 3 })]
    [InlineData("wf_slc_users_area", new[] { 500, 9, 3, -3 }, new[] { 64, 9, 3, 0 })]
    [InlineData("wf_slc_users_area", new[] { 0, 0, 1000, 1000 }, new[] { 0, 0, 64, 64 })]
    [InlineData("wf_slc_users_area", new[] { 64, 64, 2, 2 }, new[] { 64, 64, 2, 2 })]
    [InlineData("wf_slc_furni_area", new[] { -5, 9, 3, 3 }, new[] { 0, 9, 3, 3 })]
    [InlineData("wf_slc_furni_area", new[] { 5, 9, 500, -2 }, new[] { 5, 9, 64, 0 })]
    public void AreaIntsAreClampedInsteadOfRefused(string name, int[] saved, int[] stored)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. saved] };

        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var runtime));
        Assert.Equal(stored, runtime.IntParams.Take(4).ToArray());
        Assert.Equal(stored, runtime.Origin!.Native!.OwnedIntParams.ToArray());
    }
}
