using System.Text;

namespace Plus.Database.Migrator;

/// <summary>
/// Splits a SQL script into statements the way the mariadb command-line client does: on the current delimiter
/// outside quotes and comments, with DELIMITER lines changing it. Executable comments (/*! ... */) stay part of
/// their statement; statements that are only comments are dropped.
/// </summary>
public static class SqlScript
{
    public static IReadOnlyList<string> Split(string script)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var delimiter = ";";
        var hasCode = false;
        var atLineStart = true;
        var i = 0;

        void Emit()
        {
            if (hasCode) {
                var statement = current.ToString().Trim();

                if (statement.Length > 0) {
                    statements.Add(statement);
                }
            }

            current.Clear();
            hasCode = false;
        }

        while (i < script.Length) {
            var c = script[i];

            if (atLineStart && TryReadDelimiterCommand(script, i, out var newDelimiter, out var lineEnd)) {
                Emit();
                delimiter = newDelimiter;
                i = lineEnd;
                continue;
            }

            if (c == '\n') {
                atLineStart = true;
                current.Append(c);
                i++;
                continue;
            }

            if (!char.IsWhiteSpace(c)) {
                atLineStart = false;
            }

            if (string.CompareOrdinal(script, i, delimiter, 0, delimiter.Length) == 0) {
                Emit();
                i += delimiter.Length;
                continue;
            }

            switch (c) {
                case '\'':
                case '"':
                case '`':
                    i = CopyQuoted(script, i, c, current);
                    hasCode = true;
                    continue;
                case '#':
                    i = SkipLineComment(script, i, current);
                    atLineStart = true;
                    continue;
                case '-' when Next(script, i + 1) == '-' && (i + 2 >= script.Length || char.IsWhiteSpace(script[i + 2])):
                    i = SkipLineComment(script, i, current);
                    atLineStart = true;
                    continue;
                case '/' when Next(script, i + 1) == '*': {
                        var executable = Next(script, i + 2) == '!';
                        var end = script.IndexOf("*/", i + 2, StringComparison.Ordinal);
                        end = end < 0 ? script.Length : end + 2;
                        current.Append(script, i, end - i);
                        hasCode |= executable;
                        i = end;
                        continue;
                    }
            }

            if (!char.IsWhiteSpace(c)) {
                hasCode = true;
            }

            current.Append(c);
            i++;
        }

        Emit();

        return statements;
    }

    private static char Next(string script, int index) => index < script.Length ? script[index] : '\0';

    private static int CopyQuoted(string script, int start, char quote, StringBuilder into)
    {
        var i = start + 1;

        while (i < script.Length) {
            var c = script[i];

            if (c == '\\' && quote != '`') {
                i += 2;
                continue;
            }

            if (c == quote) {
                // A doubled quote is an escaped quote.
                if (Next(script, i + 1) == quote) {
                    i += 2;
                    continue;
                }

                i++;
                break;
            }

            i++;
        }

        i = Math.Min(i, script.Length);
        into.Append(script, start, i - start);

        return i;
    }

    private static int SkipLineComment(string script, int start, StringBuilder into)
    {
        var end = script.IndexOf('\n', start);
        end = end < 0 ? script.Length : end + 1;
        into.Append('\n');

        return end;
    }

    private static bool TryReadDelimiterCommand(string script, int start, out string delimiter, out int lineEnd)
    {
        delimiter = string.Empty;
        var i = start;

        while (i < script.Length && script[i] is ' ' or '\t') {
            i++;
        }

        const string keyword = "DELIMITER";
        lineEnd = script.IndexOf('\n', i);
        lineEnd = lineEnd < 0 ? script.Length : lineEnd + 1;

        if (string.Compare(script, i, keyword, 0, keyword.Length, StringComparison.OrdinalIgnoreCase) != 0 ||
            i + keyword.Length >= script.Length || !char.IsWhiteSpace(script[i + keyword.Length])) {
            return false;
        }

        delimiter = script[(i + keyword.Length)..lineEnd].Trim();

        return delimiter.Length > 0;
    }
}
