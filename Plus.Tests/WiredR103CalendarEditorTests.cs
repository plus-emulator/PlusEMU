using Xunit;
using static Plus.Tests.WiredR103EditorSave;

namespace Plus.Tests;

// Date Matches and Time Matches as Habbo stores them.
public sealed class WiredR103CalendarEditorTests
{
    private static readonly int[] Date = [0, 1, 127, 1, 1, 4095, 2026, 2026];
    private static readonly int[] Time = [0, 0, 1, 0, 0, 0, 0, 0, 23];

    private static int[] With(int[] ints, int index, int value)
    {
        var copy = (int[])ints.Clone();
        copy[index] = value;

        return copy;
    }

    [Fact]
    public void DateFlagsKeepZeroAndOneAndEveryOtherValueBecomesOne()
    {
        foreach (var index in new[] { 0, 1 }) {
            Assert.Equal(0, Saved("wf_cnd_match_date", With(Date, index, 0)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_date", With(Date, index, 1)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_date", With(Date, index, 2)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_date", With(Date, index, -1)).Ints[index]);
        }
    }

    [Fact]
    public void DateFieldsAreClampedAndTheDayRangeKeepsItsOrder()
    {
        foreach (var (index, value, stored) in new[] { (3, 0, 1), (3, -1, 1), (3, 32, 31), (3, 100, 31), (4, 0, 1), (4, 100, 31),
            (2, 128, 127), (2, 255, 127), (2, -1, 0), (5, 4096, 4095), (5, 8191, 4095), (5, -1, 0), (6, 10000, 9999), (6, -1, 0) }) {
            Assert.Equal(stored, Saved("wf_cnd_match_date", With(Date, index, value)).Ints[index]);
        }

        Assert.Equal([0, 1, 127, 31, 1, 4095, 2026, 2026], Saved("wf_cnd_match_date", [0, 1, 127, 31, 1, 4095, 2026, 2026]).Ints);
        Assert.Equal([0, 1, 127, 1, 31, 4095, 2026, 2026], Saved("wf_cnd_match_date", [0, 1, 127, 1, 31, 4095, 2026, 2026]).Ints);
        Assert.Equal([0, 1, 127, 1, 1, 4095, 9999, 9999], Saved("wf_cnd_match_date", [0, 1, 127, 1, 1, 4095, 9999, 0]).Ints);
        Assert.Equal([0, 0, 127, 1, 1, 4095, 2026, 2027], Saved("wf_cnd_match_date", [0, 0, 127, 1, 1, 4095, 2026, 2027]).Ints);
    }

    [Fact]
    public void DateMissingIntsTakeTheFrameDefaultAndExtraIntsAreDropped()
    {
        Assert.Equal([1, 0, 127, 1, 1, 4095, 0, 0], Saved("wf_cnd_match_date", [1]).Ints);
        Assert.Equal([0, 0, 127, 1, 1, 4095, 0, 0], Saved("wf_cnd_match_date", []).Ints);
        Assert.Equal(Date, Saved("wf_cnd_match_date", [.. Date, 5]).Ints);
    }

    [Theory]
    [InlineData("wf_cnd_match_date")]
    [InlineData("wf_cnd_match_time")]
    public void AValidTimezoneIsStoredAndAnInvalidOneIsStoredAsEmpty(string name)
    {
        var ints = name == "wf_cnd_match_date" ? Date : Time;

        foreach (var zone in new[] { "UTC", "Europe/Berlin", "America/New_York", "Pacific/Kiritimati", "" }) {
            Assert.Equal(zone, Saved(name, ints, text: zone).Text);
        }

        Assert.Equal("", Saved(name, ints, text: "Invalid/Zone").Text);
        Assert.Equal("", Saved(name, ints, text: new string('x', 100)).Text);
    }

    [Fact]
    public void TimeFlagsKeepZeroAndOneAndEveryOtherValueBecomesOne()
    {
        foreach (var index in new[] { 0, 1, 2 }) {
            Assert.Equal(0, Saved("wf_cnd_match_time", With(Time, index, 0)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_time", With(Time, index, 1)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_time", With(Time, index, 2)).Ints[index]);
            Assert.Equal(1, Saved("wf_cnd_match_time", With(Time, index, -1)).Ints[index]);
        }
    }

    [Fact]
    public void TimeFieldsAreClampedAndAnInvertedRangeIsStoredAsSent()
    {
        foreach (var (index, value, stored) in new[] { (3, 60, 59), (4, 100, 59), (5, -1, 0), (6, 60, 59), (7, 24, 23), (8, 100, 23), (7, -1, 0) }) {
            Assert.Equal(stored, Saved("wf_cnd_match_time", With(Time, index, value)).Ints[index]);
        }

        Assert.Equal([0, 0, 1, 59, 0, 59, 0, 23, 0], Saved("wf_cnd_match_time", [0, 0, 1, 59, 0, 59, 0, 23, 0]).Ints);
        Assert.Equal([1, 0, 0, 5, 9, 0, 0, 0, 0], Saved("wf_cnd_match_time", [1, 0, 0, 5, 9, 0, 0, 0, 0]).Ints);
    }

    [Fact]
    public void TimeMissingIntsAreZeroAndExtraIntsAreDropped()
    {
        Assert.Equal([1, 0, 0, 0, 0, 0, 0, 0, 0], Saved("wf_cnd_match_time", [1]).Ints);
        Assert.Equal([0, 0, 0, 0, 0, 0, 0, 0, 0], Saved("wf_cnd_match_time", []).Ints);
        Assert.Equal(Time, Saved("wf_cnd_match_time", [.. Time, 4]).Ints);
    }
}
