using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// How the hotel stores and refuses a save of the capturer (Text Input Variable) and the captures of a Says trigger, beyond the text output
/// rules in <see cref="WiredEditorStoreRules"/>. Observed on AIR WIN63-202610201032-107964540:
/// <list type="bullet">
/// <item>A capture name has 1 to 40 letters, digits or underscores and is stored in lower case. It is not trimmed.</item>
/// <item>The mode flag stores any non-zero value as 1 and missing ints take the default.</item>
/// <item>The capturer takes one variable of any kind, or none.</item>
/// <item>A Says text may not capture the same name twice.</item>
/// <item>A furni name output save with an empty name is refused.</item>
/// </list>
/// </summary>
internal static class WiredTextAddonRules
{
    public const string Input = "wf_xtra_text_input_variable";
    private const string FurniName = "wf_xtra_text_output_furni_name";
    private const string Output = "wf_xtra_text_output_variable";

    public const int MaxNameLength = 40;

    public const string NameLengthKey = "wiredfurni.error.placeholder.name_length";
    public const string NameSyntaxKey = "wiredfurni.error.placeholder.name_syntax";
    private const string VariableCountKey = "wiredfurni.error.invalid_variable_count";
    private const string DuplicateCaptureKey = "wiredfurni.error.capturer.duplicate";

    private static readonly Regex Capture = new("#\\(([^()#]+)\\)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool Applies(string name) => name is Input or Output;

    public static string? NameRefusal(string name) => name.Length is 0 or > MaxNameLength ? NameLengthKey
        : name.All(c => c is '_' or >= '0' and <= '9' or >= 'a' and <= 'z' or >= 'A' and <= 'Z') ? null : NameSyntaxKey;

    /// <summary>The key a new save is refused with, or null.</summary>
    public static string? Refusal(string name, WiredNativeEditorConfiguration native)
    {
        if (name == "wf_trg_says_something") {
            return CapturesTwice(native.Text) ? DuplicateCaptureKey : null;
        }

        // The furni name output has no variable, so an empty name is refused even on the box's default ints.
        if (name == FurniName) {
            return native.Text.Length == 0 ? NameLengthKey : null;
        }

        if (name != Input) {
            return null;
        }

        if (native.VariableIds.Length > 1) {
            return VariableCountKey;
        }

        // The capturer box checks the name it stores. A padded or "#"-wrapped name, which an older row keeps without the padding (see
        // StoredRow), is refused here so that it never reaches the box.
        return StoredRow(name, native).Text != native.Text ? NameRefusal(native.Text) : null;
    }

    /// <summary>
    /// The form a new save is stored in: an empty variable id is no variable, and the capturer also fits its ints and lowers its name.
    /// Stored rows keep the form they were saved in.
    /// </summary>
    public static WiredNativeEditorConfiguration StoredForm(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        native = native with { VariableIds = [.. native.VariableIds.Select(id => id.Length == 0 ? WiredVariableAbsent.WireId : id)] };

        if (name != Input) {
            return native;
        }

        var ints = native.OwnedIntParams;

        // A save with too many ints keeps them, so the editor refuses it as before.
        if (ints.Length <= defaults.Length) {
            ints = [.. defaults.Select((fallback, index) => index < ints.Length ? (ints[index] == 0 ? 0 : 1) : fallback)];
        }

        return native with
        {
            OwnedIntParams = ints,
            Text = native.Text.ToLowerInvariant()
        };
    }

    /// <summary>
    /// A capturer row saved before the name was checked as sent kept its name without padding and without surrounding "#", so it loads
    /// that way.
    /// </summary>
    public static WiredNativeEditorConfiguration StoredRow(string name, WiredNativeEditorConfiguration native)
    {
        if (name != Input) {
            return native;
        }

        var text = native.Text.Trim();

        if (text.Length >= 2 && text.StartsWith('#') && text.EndsWith('#')) {
            text = text[1..^1].Trim();
        }

        return text.Length is >= 1 and <= MaxNameLength && text.IndexOfAny(['\r', '\n', '#']) < 0 ? native with { Text = text } : native;
    }

    private static bool CapturesTwice(string text)
    {
        var names = Capture.Matches(text).Select(match => match.Groups[1].Value).ToArray();

        return names.Distinct(StringComparer.OrdinalIgnoreCase).Count() < names.Length;
    }
}
