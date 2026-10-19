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

        if (!double.IsFinite(k)) {
            return text;
        }

        var size = Math.Abs(k);

        // Observed on AIR WIN63-202610151745-318700621: a tiny height goes out as "1.0E-6"; the exponent form is used below 0.001 and
        // from 10,000,000 up, with at least one digit after the point and no "+" or leading zeros in the exponent.
        if (size != 0 && (size < 1e-3 || size >= 1e7)) {
            return Scientific(k, text);
        }

        return text.Contains('.') ? text : text + ".0";
    }

    private static string Scientific(double k, string text)
    {
        var digits = text.TrimStart('-');
        var exponent = 0;

        if (digits.IndexOf('E') is var e and >= 0) {
            exponent = int.Parse(digits[(e + 1)..], CultureInfo.InvariantCulture);
            digits = digits[..e];
        }

        var point = digits.IndexOf('.');
        var whole = point < 0 ? digits : digits[..point];
        var all = whole + (point < 0 ? "" : digits[(point + 1)..]);
        exponent += whole.Length - 1;
        var leading = all.Length - all.TrimStart('0').Length;
        all = all.Trim('0');
        exponent -= leading;

        return (k < 0 ? "-" : "") + all[0] + "." + (all.Length > 1 ? all[1..] : "0") + "E" + exponent.ToString(CultureInfo.InvariantCulture);
    }
}
