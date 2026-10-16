using System.Collections.Immutable;
using System.Globalization;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// Calendar fields use the room timezone; elapsed units always count from the UTC epoch. A Value-mode parent holds epoch
/// milliseconds, the unit of @current_time (observed on hhus, AIR WIN63-202610131318-860371703: "attr" given @current_time
/// read year 2026 and day_of_week 5 on 2026-10-16).
/// </summary>
public sealed record WiredVariableTimeUtilities(int Mask, int Mode)
{
    public const int ValidMask = 0x7FE | 0x7F00000;
    /// <summary>The creation-time and last-update modes also list the raw timestamp as "&lt;parent&gt;.&lt;mode&gt;" (bit 0).</summary>
    public const int BasePart = 0;
    public const long MaxMilliseconds = 253402300799999;
    public IEnumerable<int> Selected => Enumerable.Range(1, 26).Where(id => (Mask & (1 << id) & ValidMask) != 0);
    /// <summary>Every catalogue part of this box: the base row in modes 1 and 2, then each ticked part.</summary>
    public IEnumerable<int> Parts => Mode == 0 ? Selected : Selected.Prepend(BasePart);
    public bool Has(int id) => id is > 0 and <= 26 && (Mask & (1 << id) & ValidMask) != 0;
    public bool HasPart(int id) => id == BasePart ? Mode != 0 : Has(id);
    /// <summary>Only Value-mode parts are writable and interceptable; timestamp parts are read-only (observed on hhus).</summary>
    public bool Writable => Mode == 0;
    public static string Key(int id) => id switch
    {
        1 => "milliseconds_of_seconds",
        2 => "seconds_of_minute",
        3 => "minute_of_hour",
        4 => "hour_of_day",
        5 => "day_of_week",
        6 => "day_of_month",
        7 => "day_of_year",
        8 => "week_of_year",
        9 => "month_of_year",
        10 => "year",
        20 => "millisecond",
        21 => "second",
        22 => "minute",
        23 => "hour",
        24 => "day",
        25 => "week",
        26 => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    public string ModeKey => Mode switch
    {
        1 => "creation_time",
        2 => "last_update_time",
        _ => "value"
    };

    /// <summary>
    /// The catalogue name after "&lt;parent&gt;.": "value.year", "creation_time.year" or the base row "creation_time". Observed on hhus
    /// (AIR WIN63-202610131318-860371703): the month part is sent as "month_of_year " with a trailing space in every mode, while
    /// "@current_time.month_of_year" has none.
    /// </summary>
    public string CatalogKey(int id) => id == BasePart ? ModeKey : ModeKey + "." + (id == 9 ? "month_of_year " : Key(id));

    private static readonly ImmutableArray<KeyValuePair<int, string>> Weekdays =
        [.. new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" }.Select((name, index) => KeyValuePair.Create(index + 1, name))];
    private static readonly ImmutableArray<KeyValuePair<int, string>> Months =
        [.. new[] { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
            .Select((name, index) => KeyValuePair.Create(index + 1, name))];

    /// <summary>The text connector hhus lists on a part in every mode: day_of_week 1 Monday..7 Sunday, month_of_year 1 January..12 December.</summary>
    public static ImmutableArray<KeyValuePair<int, string>>? Connector(int id) => id switch
    {
        5 => Weekdays,
        9 => Months,
        _ => null
    };

    public long? Read(WiredVariableValue value, int id, TimeZoneInfo zone)
    {
        if (!HasPart(id)) {
            return null;
        }

        DateTimeOffset? instant = Mode switch
        {
            1 => value.CreatedAt,
            2 => value.UpdatedAt,
            _ => value.Value <= MaxMilliseconds ? DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0, value.Value)) : null
        };

        if (instant is null || Mode != 0 && instant.Value.ToUnixTimeMilliseconds() <= 0) {
            return null;
        }

        var utc = instant.Value;
        var milliseconds = utc.ToUnixTimeMilliseconds();

        if (id == BasePart) {
            return milliseconds;
        }

        var local = TimeZoneInfo.ConvertTime(utc, zone);
        long? result = id switch
        {
            1 => local.Millisecond,
            2 => local.Second,
            3 => local.Minute,
            4 => local.Hour,
            5 => local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek,
            6 => local.Day,
            7 => local.DayOfYear,
            8 => ISOWeek.GetWeekOfYear(local.DateTime),
            9 => local.Month,
            10 => local.Year,
            20 => milliseconds,
            21 => milliseconds / 1000,
            22 => milliseconds / 60000,
            23 => milliseconds / 3600000,
            24 => milliseconds / 86400000,
            25 => milliseconds / 604800000,
            26 => (utc.Year - 1970L) * 12 + utc.Month - 1,
            _ => null
        };

        return result;
    }

    /// <summary>
    /// The parent value after writing one Value-mode part, or null when the write is refused. Observed on hhus (AIR
    /// WIN63-202610131318-860371703): year 2026 → 2025 moved 2026-10-16 to 2025-10-16, and day_of_week 4 → 1 moved it to the Monday
    /// of that week. Each part moves the parent by the difference in its own unit and keeps every other field.
    /// </summary>
    public long? Write(long parent, int id, long part, TimeZoneInfo zone)
    {
        if (!Writable || !Has(id) || Read(new(parent, null, null), id, zone) is not { } current) {
            return null;
        }

        var delta = part - current;

        if (delta == 0) {
            return parent;
        }

        try {
            var utc = DateTimeOffset.FromUnixTimeMilliseconds(parent);
            var local = TimeZoneInfo.ConvertTime(utc, zone).DateTime;
            long? next = id switch
            {
                1 or 20 => checked(parent + delta),
                2 or 21 => checked(parent + delta * 1000),
                22 => checked(parent + delta * 60000),
                23 => checked(parent + delta * 3600000),
                24 => checked(parent + delta * 86400000),
                25 => checked(parent + delta * 604800000),
                26 => utc.AddMonths(checked((int)delta)).ToUnixTimeMilliseconds(),
                3 => Wall(local.AddMinutes(delta), zone),
                4 => Wall(local.AddHours(delta), zone),
                5 or 6 or 7 => Wall(local.AddDays(delta), zone),
                8 => Wall(local.AddDays(checked(delta * 7)), zone),
                9 => Wall(local.AddMonths(checked((int)delta)), zone),
                10 => Wall(local.AddYears(checked((int)delta)), zone),
                _ => null
            };

            return next is >= 0 and <= MaxMilliseconds ? next : null;
        }
        catch (ArgumentException) {
            return null;
        }
        catch (OverflowException) {
            return null;
        }
    }

    // A wall-clock time in the room zone back to epoch milliseconds; a time skipped by a clock change moves forward past the gap.
    private static long Wall(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        while (zone.IsInvalidTime(local)) {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone)).ToUnixTimeMilliseconds();
    }
}

/// <summary>
/// A read-only view of a source variable. <paramref name="Write"/>, when set, makes the part writable and interceptable: it maps the
/// source's current value and the part's new value to the source's new value.
/// </summary>
public sealed record WiredVariableDerivation(WiredVariableReference Source, Func<WiredVariableValue, WiredVariableValue?> Convert,
    bool RequiresValue = true, bool RequiresTimestamps = false, Func<long, long, long?>? Write = null)
{
    /// <summary>A creation-time part needs a source that keeps one; a global variable does not (observed on hhus).</summary>
    public bool RequiresCreationTime { get; init; }
}
