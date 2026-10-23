using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class WiredVariableTextCaptureTests
{
    [Fact]
    public void NumericAndTextCapturesRequireWholeValidValuesBeforeMatch()
    {
        WiredVariableCapturer[] capturers = [new(10, "score", null), new(11, "team", new Dictionary<int, string> { [2] = "blue team" })];
        Assert.True(WiredVariableTextCapture.TryMatch("set #score# for #team#", "SET -12 for blue team", 1, capturers, out var values));
        Assert.Equal(-12, values[10]);
        Assert.Equal(2, values[11]);
        Assert.True(WiredVariableTextCapture.TryMatch("set #score# for #team#", "set 2147483648 for blue team", 1, capturers, out values));
        Assert.Equal(2147483648L, values[10]);
        Assert.False(WiredVariableTextCapture.TryMatch("set #score# for #team#", "set 9223372036854775808 for blue team", 1, capturers, out values));
        Assert.Empty(values);
        Assert.False(WiredVariableTextCapture.TryMatch("#score#", "3 words", 1, capturers, out _));
        Assert.True(WiredVariableTextCapture.TryMatch("", "+42", 2, [capturers[0]], out values));
        Assert.Equal(42, values[10]);
    }
    [Fact]
    public void RepeatedNamesFollowStackOrdering()
    {
        Assert.True(WiredVariableTextCapture.TryMatch("#n#", "42", 1, [new(1, "n", null), new(2, "N", null)], out var values));
        Assert.False(values.ContainsKey(1));
        Assert.Equal(42, values[2]);
        Assert.False(WiredVariableTextCapture.TryMatch("#n#", new string('1', 1001), 1, [new(1, "n", null)], out _));
    }
    [Fact]
    public void AdjacentMarkersNeverMatch()
    {
        // Adjacent markers never fire, with or without a literal prefix.
        WiredVariableCapturer[] capturers = [new(10, "a", new Dictionary<int, string> { [1] = "red" }), new(11, "b", null)];
        Assert.False(WiredVariableTextCapture.TryMatch("#a##b#", "red123", 1, capturers, out _));
        WiredVariableCapturer[] numbers = [new(10, "a", null), new(11, "b", null)];
        Assert.False(WiredVariableTextCapture.TryMatch("qr:#(a)#(b)", "QR:12", 1, numbers, out _));
        Assert.False(WiredVariableTextCapture.TryMatch("qr:#(a)#(b)", "qr:#(a)#(b)", 1, numbers, out _));
        Assert.False(WiredVariableTextCapture.TryMatch("qr #(a)#(b)", "qr 12", 1, numbers, out _));
        Assert.False(WiredVariableTextCapture.TryMatch("#(a)#(b)", "12", 1, numbers, out _));
        Assert.False(WiredVariableTextCapture.TryMatch("#(a)#(b)", "12", 0, numbers, out _));
        Assert.True(WiredVariableTextCapture.TryMatch("qr:#(a)", "QR:5", 1, numbers, out var values));
        Assert.Equal(5, values[10]);
    }
    [Fact]
    public void MarkerWithoutCapturerStopsMatch()
    {
        // A marker with no capturer of its own stops the trigger.
        Assert.False(WiredVariableTextCapture.TryMatch("p7 #(a) #(b)", "p7 5 6", 1, [new(10, "a", null)], out _));
        Assert.True(WiredVariableTextCapture.TryMatch("p7 #(a) #(b)", "p7 5 6", 1, [new(10, "a", null), new(11, "b", null)], out var values));
        Assert.Equal(5, values[10]);
        Assert.Equal(6, values[11]);
        Assert.False(WiredVariableTextCapture.TryMatch("p7 #(a)", "p7 abc", 1, [new(10, "a", null)], out _));
        Assert.False(WiredVariableTextCapture.TryMatch("qr #(a) #(b)", "qr 1", 1, [new(10, "a", null), new(11, "b", null)], out _));
    }
    [Fact]
    public void ConnectorLookupIsCaseInsensitiveAndLastLineWins()
    {
        // "abc" against lines 1=abc and 2=ABC reads 2.
        WiredVariableCapturer[] capturers = [new(10, "a", new Dictionary<int, string> { [1] = "abc", [2] = "ABC" })];

        foreach (var said in new[] { "p7 ABC", "p7 Abc", "p7 abc" }) {
            Assert.True(WiredVariableTextCapture.TryMatch("p7 #(a)", said, 1, capturers, out var values));
            Assert.Equal(2, values[10]);
        }
    }
}
