using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A capturer in text mode over a variable with a Text Connector. Only a word that is a label
/// of the connector is captured, whatever the connector holds. A number is never read, an empty connector captures nothing, and the "_="
/// fallback line is not a label: neither its text nor "_" is captured.
/// </summary>
public sealed class WiredCapturerTextModeTests
{
    private static WiredVariableCapturer Capturer(string connector) => new(10, "amount", WiredVariableMetadataBox.ParseConnector(connector));

    [Theory]
    [InlineData("", "7")]
    [InlineData("", "-3")]
    [InlineData("_=other", "7")]
    [InlineData("_=other", "other")]
    [InlineData("_=other", "_")]
    [InlineData("_=", "7")]
    [InlineData("7=seven", "7")]
    [InlineData("7=seven", "9")]
    [InlineData("7=seven\n_=other", "other")]
    [InlineData("7=seven\n_=other", "9")]
    [InlineData("7=seven\n_=other", "_")]
    public void AWordThatIsNotALabelIsNotCaptured(string connector, string word)
    {
        Assert.False(WiredVariableTextCapture.TryMatch("tc price #(amount)", "tc price " + word, 1, [Capturer(connector)], out var values));
        Assert.Empty(values);
    }

    [Theory]
    [InlineData("7=seven", "seven", 7)]
    [InlineData("7=seven", "Seven", 7)]
    [InlineData("7=seven\n_=other", "seven", 7)]
    [InlineData("7=seven\n_=", "SEVEN", 7)]
    public void ALabelIsCapturedWhateverTheCaseAndTheFallback(string connector, string word, long expected)
    {
        Assert.True(WiredVariableTextCapture.TryMatch("tc price #(amount)", "tc price " + word, 1, [Capturer(connector)], out var values));
        Assert.Equal(expected, values[10]);
    }

    // No labels at all is number mode. A text-mode capturer without a connector gets an empty connector and captures nothing
    // (see PlacedFurniRoomTests.ATextModeCapturerWithoutAConnectorCapturesNothing).
    [Fact]
    public void InNumberModeTheCapturerReadsNumbers()
    {
        Assert.True(WiredVariableTextCapture.TryMatch("tc price #(amount)", "tc price 7", 1, [new(10, "amount", null)], out var values));
        Assert.Equal(7, values[10]);
    }
}
