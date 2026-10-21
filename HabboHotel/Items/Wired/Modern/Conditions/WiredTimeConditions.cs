using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Modern.Conditions;

/// <summary>Calendar predicates over the compiled current editor settings.</summary>
public static class WiredTimeConditions
{
    public static bool MatchesTime(WiredConfiguration config, DateTimeOffset roomLocalTime)
    {
        roomLocalTime = InConfiguredZone(config, roomLocalTime);

        return MatchesPart(roomLocalTime.Hour, Param(config, 0), Param(config, 1), Param(config, 2))
        && MatchesPart(roomLocalTime.Minute, Param(config, 3), Param(config, 4), Param(config, 5))
        && MatchesPart(roomLocalTime.Second, Param(config, 6), Param(config, 7), Param(config, 8));
    }

    public static bool MatchesDate(WiredConfiguration config, DateTimeOffset roomLocalTime)
    {
        roomLocalTime = InConfiguredZone(config, roomLocalTime);
        var weekdayMask = Param(config, 0, 127);
        var monthMask = Param(config, 4, 4095);
        // Volt weekdays are Monday=0 through Sunday=6.
        var weekday = ((int)roomLocalTime.DayOfWeek + 6) % 7;

        // Observed on AIR WIN63-202610201032-107964540: a mask with no bit set fails the condition.
        return (weekdayMask & (1 << weekday)) != 0
            && (monthMask & (1 << (roomLocalTime.Month - 1))) != 0
            && MatchesPart(roomLocalTime.Day, Param(config, 1), Param(config, 2, 1), Param(config, 3, 31))
            && MatchesPart(roomLocalTime.Year, Param(config, 5), Param(config, 6), Param(config, 7, 9999));
    }

    public static bool MatchesRange(WiredConfiguration config, DateTimeOffset now)
    {
        var start = Param(config, 0);
        var end = Param(config, 1);
        var seconds = now.ToUnixTimeSeconds();

        // Observed on hhus (AIR WIN63-202610131318-860371703): a start of 0 is open, an end of 0 never passes: (0, future) passed,
        // (0, 0), (past, 0), (future, 0), (0, past) and (past, past) failed, (past, future) passed.
        return (start == 0 || seconds >= start) && end != 0 && seconds <= end;
    }

    public static bool MatchesElapsed(WiredConfiguration config, long elapsedMs, bool moreThan)
    {
        var elapsedPulses = Math.Max(0, elapsedMs) / 500;
        var targetPulses = Param(config, 0, 1);

        // Observed on AIR WIN63-202610201032-107964540: both conditions pass during the pulse of their threshold. Less Than passes
        // through it and More Than from it.
        return moreThan ? elapsedPulses >= targetPulses : elapsedPulses <= targetPulses;
    }

    public static bool MatchesCounter(WiredConfiguration config, IEnumerable<long> currentTimesMs)
    {
        var targetMs = Param(config, 1) * 60_000L + Param(config, 2) * 500L;

        return WiredRoomOperations.Quantify(currentTimesMs.Select(value =>
            WiredRoomOperations.Compare(value, targetMs, Param(config, 0, 1))), Param(config, 4));
    }

    public static bool MatchesPart(int value, int mode, int from, int to) => mode switch
    {
        0 => true,
        1 => value == from,
        2 => from <= to ? value >= from && value <= to : value >= from || value <= to,
        _ => false
    };

    internal static bool IsValidZone(string zone)
    {
        if (zone.Length == 0) {
            return true;
        }

        try {
            _ = TimeZoneInfo.FindSystemTimeZoneById(zone);

            return true;
        }
        catch (TimeZoneNotFoundException) {
            return false;
        }
        catch (InvalidTimeZoneException) {
            return false;
        }
    }

    private static DateTimeOffset InConfiguredZone(WiredConfiguration config, DateTimeOffset instant) =>
        config.Text.Length == 0 ? instant : TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(config.Text));

    private static int Param(WiredConfiguration config, int index, int fallback = 0) =>
        index < config.IntParams.Length ? config.IntParams[index] : fallback;
}
