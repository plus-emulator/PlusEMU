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
