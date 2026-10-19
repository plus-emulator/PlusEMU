using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Xunit;

namespace Plus.Tests;

/// <summary>Observed on AIR WIN63-202610151745-318700621: Users By Name splits on tabs only, keeps spaces and stores lowercase.</summary>
public sealed class WiredUsersByNameTests
{
    [Theory]
    [InlineData("ada\tbob", new[] { 1, 2 })]
    [InlineData("Ada\t\tBOB", new[] { 1, 2 })]
    [InlineData("\tbob", new[] { 2 })]
    [InlineData(" ada", new int[0])]
    [InlineData("ada ", new int[0])]
    [InlineData("ada\nbob", new int[0])]
    [InlineData("ada,bob", new int[0])]
    public void NamesAreTabSeparatedAndKeepTheirSpaces(string text, int[] users)
    {
        var selection = WiredSelectorModule.SelectRaw("wf_slc_users_byname", WiredSelectorTests.Config(text: text), WiredSelectorTests.World(),
            WiredSelectorTests.Inputs()).Selection;

        Assert.Equal(users, selection.UserIds.Order());
    }

    [Fact]
    public void SavedNamesAreStoredLowercase()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_users_byname", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = "Robbie\tJohnDoe" };

        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var runtime));
        Assert.Equal("robbie\tjohndoe", runtime.Origin!.Native!.Text);
        Assert.Equal("robbie\tjohndoe", runtime.Text);
    }
}
