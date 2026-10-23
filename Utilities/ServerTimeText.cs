using System.Globalization;

namespace Plus.Utilities;

/// <summary>
/// Wall-clock times the client sends as text, e.g. GetSecondsUntil's "2026-11-05 23:59". They are read in the server's time zone.
/// </summary>
public static class ServerTimeText
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm", "yyyy-MM-dd H:mm:ss",
        "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd"
    ];

    public static DateTimeOffset? ParseLocal(string text, TimeZoneInfo zone)
    {
        if (!DateTime.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local)) {
            return null;
        }

        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <summary>Whole seconds from <paramref name="now"/> until the time; past times are negative and unreadable text is 0.</summary>
    public static int SecondsUntil(string text, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (ParseLocal(text, zone) is not { } target) {
            return 0;
        }

        var seconds = Math.Floor((target - now).TotalSeconds);

        return (int)Math.Clamp(seconds, int.MinValue, int.MaxValue);
    }
}
