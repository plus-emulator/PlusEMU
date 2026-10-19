using Plus.Utilities;
using Xunit;

namespace Plus.Tests;

public class TextHandlingTests
{
    // Observed on AIR WIN63-202610151745-318700621: heights go out in the shortest form with ".0" on whole numbers, and a tiny height
    // as "1.0E-6", with an exponent below 0.001 and from 10,000,000 up.
    [Theory]
    [InlineData(0.0, "0.0")]
    [InlineData(1.0, "1.0")]
    [InlineData(0.65, "0.65")]
    [InlineData(1.73125, "1.73125")]
    [InlineData(0.001, "0.001")]
    [InlineData(1e-6, "1.0E-6")]
    [InlineData(0.0005, "5.0E-4")]
    [InlineData(1.5e-5, "1.5E-5")]
    [InlineData(-2e-6, "-2.0E-6")]
    [InlineData(9999999.0, "9999999.0")]
    [InlineData(12345678.0, "1.2345678E7")]
    [InlineData(1e15, "1.0E15")]
    public void HeightsAreWrittenTheWayHabboWritesThem(double value, string expected) =>
        Assert.Equal(expected, TextHandling.GetString(value));
}
