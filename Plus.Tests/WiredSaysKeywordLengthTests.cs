using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>Observed on hhus (AIR WIN63-202610131318-860371703): Says Something keeps the first 200 characters of a longer keyword.</summary>
public class WiredSaysKeywordLengthTests
{
    private static WiredConfiguration Compile(int length)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_says_something", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = new string('k', length) };
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out var runtime));

        return runtime;
    }

    [Theory]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(300, 200)]
    [InlineData(150, 150)]
    public void KeywordIsStoredUpTo200Characters(int saved, int kept)
    {
        var runtime = Compile(saved);

        Assert.Equal(kept, runtime.Text.Length);
        Assert.Equal(kept, runtime.Origin!.Native!.Text.Length);
    }
}
