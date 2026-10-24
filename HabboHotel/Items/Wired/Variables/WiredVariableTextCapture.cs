using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>A capture of one context variable. Labels is the Text Connector in text mode (empty without one) and null in number mode.</summary>
public sealed record WiredVariableCapturer(uint DefinitionId, string Name, IReadOnlyDictionary<long, string>? Labels)
{
    public long? Read(string text)
    {
        // With a connector in text mode only its labels are captured. A number is never read, and a connector with no labels, or with only the "_=" fallback, captures nothing.
        if (Labels is not null) {
            // The connector lookup is case-insensitive and the last matching line wins.
            return Labels.Where(pair => string.Equals(pair.Value.Trim(), text.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(pair => (long?)pair.Key).LastOrDefault();
        }

        // A space before or after the number is not read ("k 5 " and "k  5" do not fire).
        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}

/// <summary>Bounded template matching. Resolve every capture before publishing any context values.</summary>
public static class WiredVariableTextCapture
{
    // The capturer is written "#(name)", as the editor's preview tells the player; "#name#" is the older spelling this box also reads.
    private static readonly Regex Placeholder = new("#\\(([^()#]+)\\)|#([^#()]+)#", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    public static bool TryMatch(string template, string text, int mode, IReadOnlyList<WiredVariableCapturer> capturers,
        out IReadOnlyDictionary<uint, long> values)
    {
        values = new Dictionary<uint, long>();

        if (template.Length > 5000 || text.Length > 1000 || capturers.Count > 100 || mode is < 0 or > 2) {
            return false;
        }

        var byName = new Dictionary<string, WiredVariableCapturer>(StringComparer.OrdinalIgnoreCase);

        foreach (var capturer in capturers) {
            byName[capturer.Name] = capturer; // Ordered stack: last duplicate name wins.
        }

        template = template.Trim();

        if (mode == 2 && template.Length == 0) {
            if (byName.Count != 1 || byName.Values.First().Read(text) is not { } value) {
                return false;
            }

            values = new Dictionary<uint, long> { [byName.Values.First().DefinitionId] = value };

            return true;
        }

        var matches = Placeholder.Matches(template).Cast<Match>().ToArray();

        if (matches.Length is 0 or > 8) {
            return Literal(template, text, mode);
        }

        var names = matches.Select(match => (match.Groups[1].Success ? match.Groups[1] : match.Groups[2]).Value.Trim()).ToArray();

        // A marker without a capturer of its own stops the trigger.
        if (!names.All(byName.ContainsKey)) {
            return false;
        }

        var literals = new List<string>();
        var cursor = 0;

        for (var index = 0; index < matches.Length; index++) {
            var match = matches[index];

            // Adjacent markers never fire, with or without a literal prefix.
            if (index > 0 && match.Index == cursor) {
                return false;
            }

            literals.Add(template[cursor..match.Index]);
            cursor = match.Index + match.Length;
        }

        literals.Add(template[cursor..]);
        var pattern = new StringBuilder(Regex.Escape(literals[0]));

        for (var index = 0; index < matches.Length - 1; index++) {
            pattern.Append("(?>(.+?)").Append(Regex.Escape(literals[index + 1])).Append(')');
        }

        var head = pattern.ToString();
        var tail = Regex.Escape(literals[^1]);

        // A keyword that is only part of the line ("the price #(p) please") hands over the typed word first, as the line goes on after it;
        // a line that is the keyword, or a word that cannot be read, hands over the rest of the line, so a text of several words still reads.
        string[] lasts = mode == 0 ? ["(\\S+)", "(.+)"] : ["(.+)"];

        try {
            foreach (var last in lasts) {
                var expression = mode == 0 ? head + last + tail : "\\A" + head + last + tail + "\\z";
                var match = Regex.Match(text, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25));

                if (!match.Success) {
                    continue;
                }

                var captured = new Dictionary<uint, long>();
                var readable = true;

                for (var index = 0; index < names.Length && readable; index++) {
                    var capturer = byName[names[index]];

                    if (capturer.Read(match.Groups[index + 1].Value) is { } value) {
                        captured[capturer.DefinitionId] = value;
                    }
                    else {
                        readable = false;
                    }
                }

                if (readable) {
                    values = captured;

                    return true;
                }
            }

            return false;
        }
        catch (RegexMatchTimeoutException) {
            return false;
        }
    }
    private static bool Literal(string template, string text, int mode) => template.Length > 0 && (mode == 0
        ? text.Trim().Contains(template, StringComparison.OrdinalIgnoreCase) : string.Equals(text.Trim(), template, StringComparison.OrdinalIgnoreCase));
}
