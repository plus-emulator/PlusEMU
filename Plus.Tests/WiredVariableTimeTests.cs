using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class WiredVariableTimeTests
{
    [Theory]
    [InlineData(1, 1234)]
    [InlineData(2, 2678)]
    public void TimestampModesPreserveMilliseconds(int mode, int expectedMilliseconds)
    {
        var time = new WiredVariableTimeUtilities(WiredVariableTimeUtilities.ValidMask, mode);
        var value = new WiredVariableValue(99, DateTimeOffset.FromUnixTimeMilliseconds(1234), DateTimeOffset.FromUnixTimeMilliseconds(2678));
        Assert.Equal(expectedMilliseconds % 1000, time.Read(value, 1, TimeZoneInfo.Utc));
        Assert.Equal(expectedMilliseconds, time.Read(value, 20, TimeZoneInfo.Utc));
        Assert.Equal(expectedMilliseconds / 1000, time.Read(value, 21, TimeZoneInfo.Utc));
    }

    [Fact]
    public void CalendarUsesExplicitRoomTimezoneAndElapsedUnitsUseUtc()
    {
        var time = new WiredVariableTimeUtilities(WiredVariableTimeUtilities.ValidMask, 0);
        var value = new WiredVariableValue(1609459200000, null, null); // 2021-01-01 00:00 UTC, ISO week 53, in epoch milliseconds.
        var zone = TimeZoneInfo.CreateCustomTimeZone("probe", TimeSpan.FromHours(-5), "probe", "probe");
        Assert.Equal(19, time.Read(value, 4, zone));
        Assert.Equal(31, time.Read(value, 6, zone));
        Assert.Equal(2020, time.Read(value, 10, zone));
        Assert.Equal(53, time.Read(value, 8, zone));
        Assert.Equal(612, time.Read(value, 26, zone));
        Assert.Equal(1609459200, time.Read(value, 21, zone));
        Assert.Equal(1609459200000L, time.Read(value, 20, zone));
        Assert.Equal(0, time.Read(value with { Value = -1 }, 21, zone));
    }

    [Fact]
    public void PartsReadAndWriteBefore1970AsObservedOnHhus()
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703), Inspection reads after writing the parent or one part.
        var zone = Plus.HabboHotel.Items.Wired.Settings.WiredRoomSettings.DefaultTimeZone;
        var time = new WiredVariableTimeUtilities(WiredVariableTimeUtilities.ValidMask, 0);
        long? Read(long parent, int part) => time.Read(new(parent, null, null), part, zone);
        Assert.Equal((999L, 59L, 59L, 0L, 4L, 1L, 1970L), (Read(-1, 1), Read(-1, 2), Read(-1, 3), Read(-1, 4), Read(-1, 5), Read(-1, 6), Read(-1, 10)));
        Assert.Equal((-1L, 0L, 0L, 0L, 0L, 0L), (Read(-1, 20), Read(-1, 21), Read(-1, 22), Read(-1, 23), Read(-1, 24), Read(-1, 26)));
        Assert.Equal((31L, 12L, 1969L, -86399L, -1439L, -23L, 0L, 0L, 0L),
            (Read(-86399000, 6), Read(-86399000, 9), Read(-86399000, 10), Read(-86399000, 21), Read(-86399000, 22), Read(-86399000, 23),
                Read(-86399000, 24), Read(-86399000, 25), Read(-86399000, 26)));
        Assert.Equal(-86399000L, time.Write(1000, 5, 3, zone));
    }

    [Theory]
    [InlineData(1, 500, 1500L)]
    [InlineData(2, 30, 30000L)]
    [InlineData(3, 45, 2701000L)]
    [InlineData(4, 13, 43201000L)]
    [InlineData(6, 15, 1209601000L)]
    [InlineData(7, 100, 8553601000L)]
    [InlineData(8, 10, 5443201000L)]
    [InlineData(9, 6, 13046401000L)]
    [InlineData(10, 2000, 946688401000L)]
    [InlineData(20, 5000, 5000L)]
    [InlineData(21, 100, 100000L)]
    [InlineData(22, 100, 6001000L)]
    [InlineData(23, 100, 360001000L)]
    [InlineData(24, 100, 8640001000L)]
    [InlineData(25, 10, 6048001000L)]
    [InlineData(26, 10, 26265601000L)]
    public void WritingAPartFromOneSecondAfterTheEpochMatchesHhus(int part, long value, long parent)
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703): the parent set to 1000, then one part written through the Inspection tab;
        // the parts read back as these parents (the int parent itself reads clamped above 2147483647).
        var zone = Plus.HabboHotel.Items.Wired.Settings.WiredRoomSettings.DefaultTimeZone;
        var time = new WiredVariableTimeUtilities(WiredVariableTimeUtilities.ValidMask, 0);
        Assert.Equal(parent, time.Write(1000, part, value, zone));
    }

    [Fact]
    public void TheDefaultWiredCalendarIsLondonTimeAsObservedOnHhus()
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703): a parent of 0 read hour_of_day 1 on a Thursday in 1970, and
        // @current_time.hour_of_day read 22 at 21:26 UTC on 2026-10-16.
        var zone = Plus.HabboHotel.Items.Wired.Settings.WiredRoomSettings.DefaultTimeZone;
        var time = new WiredVariableTimeUtilities(WiredVariableTimeUtilities.ValidMask, 0);
        Assert.Equal((1, 4, 1, 1970), (time.Read(new(0, null, null), 4, zone), time.Read(new(0, null, null), 5, zone),
            time.Read(new(0, null, null), 6, zone), time.Read(new(0, null, null), 10, zone)));
        var october = new DateTimeOffset(2026, 10, 16, 21, 26, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        Assert.Equal((22, 5), (time.Read(new(october, null, null), 4, zone), time.Read(new(october, null, null), 5, zone)));
    }

    [Fact]
    public void OnlyDayOfWeekAndMonthOfYearCarryTextConnectors()
    {
        Assert.Equal(["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"],
            WiredVariableTimeUtilities.Connector(5)!.Value.Select(pair => pair.Value));
        Assert.Equal(Enumerable.Range(1, 7), WiredVariableTimeUtilities.Connector(5)!.Value.Select(pair => pair.Key));
        Assert.Equal(Enumerable.Range(1, 12), WiredVariableTimeUtilities.Connector(9)!.Value.Select(pair => pair.Key));
        Assert.Equal(("January", "December"), (WiredVariableTimeUtilities.Connector(9)!.Value[0].Value, WiredVariableTimeUtilities.Connector(9)!.Value[11].Value));
        Assert.All(Enumerable.Range(0, 27).Where(id => id is not (5 or 9)), id => Assert.Null(WiredVariableTimeUtilities.Connector(id)));
    }
    [Fact]
    public void TimestampModesRequirePresentTimestampsAndRespectSelectedFields()
    {
        var creation = new WiredVariableTimeUtilities(1 << 21, 1);
        var updated = creation with { Mode = 2 };
        var value = new WiredVariableValue(99, DateTimeOffset.FromUnixTimeMilliseconds(12001), DateTimeOffset.FromUnixTimeMilliseconds(24001));
        Assert.Equal(12, creation.Read(value, 21, TimeZoneInfo.Utc));
        Assert.Equal(24, updated.Read(value, 21, TimeZoneInfo.Utc));
        Assert.Null(creation.Read(value with { CreatedAt = null }, 21, TimeZoneInfo.Utc));
        Assert.Null(creation.Read(value, 10, TimeZoneInfo.Utc));
        Assert.Null(WiredRoomVariables.SyntheticId(WiredVariableTarget.User, 6250000, 0, false));
        Assert.Equal(700000161u, WiredRoomVariables.SyntheticId(WiredVariableTarget.User, 10, 0, false));
        Assert.Equal(1500000341u, WiredRoomVariables.SyntheticId(WiredVariableTarget.User, 10, 21, true));
    }
}
