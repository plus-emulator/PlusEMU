using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A row stored before the Habbo placeholder rules keeps loading as it was saved.
public sealed class WiredR105StoredRowTests
{
    [Theory]
    [InlineData("wf_xtra_text_output_variable", new[] { 1, 1, 0 }, "Tv.Name\t,,,,,,,,")]
    [InlineData("wf_xtra_text_output_variable", new[] { 0, 1, 0 }, "tv\t,")]
    [InlineData("wf_xtra_text_output_furni_name", new[] { 1 }, "Tv Name\t,,,,,,,,")]
    public void AStoredTextOutputRowIsReadAsItWasSaved(string name, int[] ints, string text)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. ints], Text = text };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime, storedRow: true));
        Assert.Equal(text, runtime.Origin!.Native.Text);
    }
}
