using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Xunit;

namespace Plus.Tests;

public class WiredDateMaskTests
{
    private static readonly DateTimeOffset Friday = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static bool Matches(int weekdays, int months)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_match_date", out var date));
        var native = WiredNativeEditorProjection.DefaultNative(date) with { OwnedIntParams = [0, 0, weekdays, 1, 31, months, 2026, 2026] };
        Assert.True(WiredNativeEditorProjection.TryCompile(1, date, native, out var config));

        return WiredTimeConditions.MatchesDate(config, Friday);
    }

    // A weekday mask with no day set fails the condition, and so does a month mask with no month set.
    [Fact]
    public void AnEmptyWeekdayMaskFails() => Assert.False(Matches(0, 4095));

    [Fact]
    public void AnEmptyMonthMaskFails() => Assert.False(Matches(127, 0));

    [Fact]
    public void AFullWeekdayAndMonthMaskPass() => Assert.True(Matches(127, 4095));

    [Fact]
    public void ASetWeekdayAndMonthPass() => Assert.True(Matches(1 << 4, 1 << 9));
}
