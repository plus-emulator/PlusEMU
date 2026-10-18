using System.Globalization;

namespace Plus.Utilities;

public static class TextHandling
{
    /// <summary>
    /// A height or position as Habbo writes it: the shortest form, with ".0" on whole numbers. Observed on AIR
    /// WIN63-202610151745-318700621: room entry objects, user updates and walk statuses carry "0.0", "1.0", "10.0", "0.5", "0.65" and
    /// "1.73125". The client reads these strings with Number() (package_71/class_2766.as:81), so this is about matching the wire, not
    /// about how the client draws.
    /// </summary>
    public static string GetString(double k)
    {
        var text = k.ToString(CultureInfo.InvariantCulture);

        return double.IsFinite(k) && !text.Contains('.') && !text.Contains('E') ? text + ".0" : text;
    }
}
