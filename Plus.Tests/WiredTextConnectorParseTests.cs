using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public class WiredTextConnectorParseTests
{
    // Observed on AIR WIN63-202610151745-318700621: the save always succeeds and the stored connector keeps only "number=text" lines.
    [Theory]
    [InlineData("7=Seven\n8=seven", "7:Seven|8:seven")]
    [InlineData("7=a\n7=b", "7:b")]
    [InlineData("1=x\nbad\n2=y", "1:x|2:y")]
    [InlineData(" 3 = z ", "")]
    [InlineData("-1=neg", "-1:neg")]
    [InlineData("x=1", "")]
    [InlineData("5=", "5:")]
    [InlineData("Seven=7\nseven=8", "")]
    public void KeepsOnlyNumberEqualsTextLines(string text, string expected)
    {
        var parsed = WiredVariableMetadataBox.ParseConnector(text);
        Assert.Equal(expected, string.Join("|", parsed.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}:{pair.Value}")));
    }

    // Observed on AIR WIN63-202610151745-318700621 (r97_tc_store): the stored text is the valid lines, one per number, in number order.
    [Theory]
    [InlineData("9=a\n3=b\n-2=neg\n20=c", "-2=neg\n3=b\n9=a\n20=c")]
    [InlineData("Seven=7\nseven=8\n7=seven\n 8=x\n9=\n10=a=b\n7=again", "7=again\n9=\n10=a=b")]
    public void StoresTheValidLinesInNumberOrder(string text, string stored) =>
        Assert.Equal(stored, WiredVariableMetadataBox.NormalizeConnector(text));

    [Fact]
    public void TheSavedConnectorReadsBackNormalised()
    {
        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredBoxRegistry.TryGet("wf_xtra_var_text_connector", out var descriptor));
        var native = Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = "9=a\n3=b" };
        Assert.True(Plus.HabboHotel.Items.Wired.Configuration.WiredNativeEditorProjection.TryCompile(1, descriptor, native, out var runtime));
        Assert.Equal("3=b\n9=a", runtime.Origin!.Native.Text);
    }

    // Observed on AIR WIN63-202610201032-107964540: a line "_=text" is the fallback text. The last one wins, the text may be
    // empty and may hold "=", and a line with spaces around the "_" is dropped like any other bad line.
    [Theory]
    [InlineData("7=seven\n_=other", "other")]
    [InlineData("_=other", "other")]
    [InlineData("_=", "")]
    [InlineData("_=[Empty]", "[Empty]")]
    [InlineData("_=a\n_=b", "b")]
    [InlineData("_=x\n_=", "")]
    [InlineData("_=a=b", "a=b")]
    [InlineData("7=seven\n\n_=o", "o")]
    [InlineData(" _ = x ", null)]
    [InlineData("_ =x", null)]
    [InlineData(" _=x", null)]
    [InlineData("7=seven", null)]
    public void ReadsTheFallbackLine(string text, string? fallback) =>
        Assert.Equal(fallback, WiredVariableMetadataBox.ParseConnector(text).Fallback);

    // Observed on AIR WIN63-202610201032-107964540: entries are stored in number order and the fallback is written last.
    [Theory]
    [InlineData("_=other\n7=seven", "7=seven\n_=other")]
    [InlineData("7=seven\n_=other\n9=nine", "7=seven\n9=nine\n_=other")]
    [InlineData("_=x\n_=", "_=")]
    [InlineData("_=a=b", "_=a=b")]
    [InlineData(" _=x", "")]
    public void StoresTheFallbackAfterTheEntries(string text, string stored) =>
        Assert.Equal(stored, WiredVariableMetadataBox.NormalizeConnector(text));
}
