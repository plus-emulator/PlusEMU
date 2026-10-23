using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>Saves that echo the frame's trailing hotel int are accepted.</summary>
public class WiredHotelIntSaveTests
{
    private static bool Compile(string name, int[] ints, out WiredConfiguration runtime)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. ints] };

        return WiredNativeEditorProjection.TryCompile(7, descriptor, native, out runtime);
    }

    [Theory]
    [InlineData("wf_act_move_rotate", new[] { 0, 0, 1 }, new[] { 0, 0 })]
    [InlineData("wf_act_move_rotate", new[] { 1, 2, 0 }, new[] { 1, 2 })]
    [InlineData("wf_act_match_to_sshot", new[] { 1, 0, 1, 0, 1 }, new[] { 1, 0, 1, 0 })]
    public void TheTrailingHotelIntIsAcceptedAndDropped(string name, int[] saved, int[] kept)
    {
        Assert.True(Compile(name, saved, out var runtime));
        Assert.Equal(kept, runtime.Origin!.Native!.OwnedIntParams.ToArray());
        Assert.True(Compile(name, kept, out _));
    }

    [Fact]
    public void AnythingElseAfterTheOwnedFieldsIsStillRefused()
    {
        Assert.False(Compile("wf_act_move_rotate", [0, 0, 2], out _));
        Assert.False(Compile("wf_act_move_rotate", [0, 0, 1, 1], out _));
    }
}
