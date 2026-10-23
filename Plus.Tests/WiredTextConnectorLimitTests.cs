using Xunit;

namespace Plus.Tests;

/// <summary>
/// The Text Connector text limit, driven through the raw save packet. Observed on AIR WIN63-202610201032-107964540: the limit is 1000
/// UTF-8 bytes of the whole text (the "_=" fallback line counts), a longer text is refused with wiredfurni.error.too_much_text and the
/// stored text stays as it was, and there is no limit on the number of lines.
/// </summary>
public sealed class WiredTextConnectorLimitTests
{
    private const string Box = "wf_xtra_var_text_connector";
    private const string TooMuchText = "wiredfurni.error.too_much_text";

    private static string Pad(string prefix, int bytes, char fill = 'y') =>
        prefix + new string(fill, (bytes - System.Text.Encoding.UTF8.GetByteCount(prefix)) / System.Text.Encoding.UTF8.GetByteCount(fill.ToString()));

    [Theory]
    [InlineData("7=", 999)]
    [InlineData("7=", 1000)]
    [InlineData("_=", 1000)]
    [InlineData("7=seven\n_=", 1000)]
    public void ATextUpToThe1000ByteLimitIsStoredWhole(string prefix, int bytes)
    {
        using var save = new WiredRawSave(Box);
        var text = Pad(prefix, bytes);
        var result = save.Save(text: text);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(text, result.Text);
    }

    [Theory]
    [InlineData("7=", 1001)]
    [InlineData("7=", 1100)]
    [InlineData("_=", 1001)]
    [InlineData("7=seven\n_=", 1001)]
    [InlineData("7=", 8192)]
    public void ATextOverTheLimitIsRefusedAndTheStoredTextStays(string prefix, int bytes)
    {
        using var save = new WiredRawSave(Box);
        Assert.True(save.Save(text: "0=zero").Accepted);
        var result = save.Save(text: Pad(prefix, bytes));

        Assert.False(result.Accepted, result.ToString());
        Assert.Equal(TooMuchText, result.Key);
        Assert.Equal("1000", Assert.Contains("limit", result.ErrorParameters!));
        Assert.Equal("0=zero", save.Stored().Text);
    }

    // The limit counts UTF-8 bytes: 2-byte and 3-byte characters use it up faster than their length suggests.
    [Theory]
    [InlineData('é', 1000, true)]
    [InlineData('é', 1002, false)]
    [InlineData('€', 998, true)]
    [InlineData('€', 1001, false)]
    public void TheLimitCountsUtf8BytesNotCharacters(char fill, int bytes, bool accepted)
    {
        using var save = new WiredRawSave(Box);
        var text = Pad("7=", bytes, fill);
        var result = save.Save(text: text);

        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(accepted ? text : "", save.Stored().Text);

        if (!accepted) {
            Assert.Equal(TooMuchText, result.Key);
        }
    }

    [Theory]
    [InlineData(31, false)]
    [InlineData(60, false)]
    [InlineData(150, false)]
    [InlineData(149, true)]
    public void TheNumberOfLinesIsNotLimited(int lines, bool fallback)
    {
        using var save = new WiredRawSave(Box);
        var text = string.Join('\n', Enumerable.Range(0, lines).Select(index => $"{index}=a")) + (fallback ? "\n_=z" : "");
        var result = save.Save(text: text);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(text, result.Text);
    }

    // A row stored by an older server may be longer than a save is now allowed to be; it must keep loading.
    [Fact]
    public void AStoredTextOverTheLimitStillParses()
    {
        var text = "7=" + new string('é', 700) + "\n_=" + new string('y', 400);
        var parsed = Plus.HabboHotel.Items.Wired.Variables.WiredVariableMetadataBox.ParseConnector(text);

        Assert.Equal(700, parsed[7].Length);
        Assert.Equal(400, parsed.Fallback!.Length);
    }
}
