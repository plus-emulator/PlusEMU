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
}
