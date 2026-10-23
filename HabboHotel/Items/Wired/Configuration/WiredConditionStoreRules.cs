using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these boxes. A save with an odd value is not refused: the value is clamped, switched to the
/// nearest flag value, or filled from the frame default, and the box reads back the stored form.
/// </summary>
internal static class WiredConditionStoreRules
{
    /// <param name="Ints">The stored form of the ints.</param>
    /// <param name="Text">The stored form of the text, or null to keep it as sent.</param>
    /// <param name="FitsToFrame">False when the ints are normalised as sent, without the frame default fill.</param>
    /// <param name="KeepsQuantifierTwo">The condition keeps a quantifier of 2 as sent, also when its editor shows no quantifier.</param>
    private sealed record Rule(Func<ImmutableArray<int>, ImmutableArray<int>> Ints, Func<string, string>? Text = null,
        bool FitsToFrame = true, bool KeepsQuantifierTwo = false);

    // Each box is named once, with its normal form and flags. The ints are cut and padded to the frame before the rule runs.
    private static readonly FrozenDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        { "wf_cnd_actor_dir", new(p => [Math.Clamp(p[0], 0, 255)]) },
        { "wf_cnd_actor_in_team", new(Team) },
        { "wf_cnd_not_in_team", new(Team) },
        // Only the comparison keeps values the editor never offers: 3 is stored (the upper end of the comparison is not clamped).
        { "wf_cnd_counter_time_matches", new(p => [Math.Clamp(p[0], 0, 59), Math.Clamp(p[1], 0, 99), Math.Clamp(p[2], 0, 1), Math.Max(p[3], 0)]) },
        { "wf_cnd_furnis_hv_avtrs", new(OnOff) },
        { "wf_cnd_not_furni_on", new(OnOff) },
        { "wf_cnd_not_hv_avtrs", new(OnOff) },
        { "wf_cnd_has_altitude", new(p => [Math.Clamp(p[0], 0, 8000), Math.Max(p[1], 0)]) },
        { "wf_cnd_match_snapshot", new(OnlyOne) },
        { "wf_cnd_not_match_snap", new(OnlyOne) },
        { "wf_cnd_not_user_count", new(UserCount) },
        // User Count In also keeps a quantifier of 1 or 2 although its editor shows none.
        { "wf_cnd_user_count_in", new(UserCount, KeepsQuantifierTwo: true) },
        { "wf_cnd_match_date", new(MatchDate, ZoneOrEmpty) },
        { "wf_cnd_match_time", new(MatchTime, ZoneOrEmpty) },
        // Move Furni As Group: the target flag is 0 or 1 and each offset is clamped to the editor's -64..64.
        { "wf_act_move_furni_as_group", new(p => [Flag(p[0]), Math.Clamp(p[1], -64, 64), Math.Clamp(p[2], -64, 64)]) },
        // The two split flags of a signal count only when they are exactly 1.
        { "wf_act_neg_send_signal", new(OnlyOne) },
        // Selection Quantity: the user or furni flag is 0 or 1, the amount 0..100, and the comparison keeps 3 and 4.
        { "wf_cnd_slc_quantity", new(p => [p[0] is 0 or 1 ? p[0] : 0, Math.Clamp(p[1], 0, 100), Math.Max(p[2], 0)], KeepsQuantifierTwo: true) },
        { "wf_cnd_team_has_rank", new(p => [Math.Clamp(p[0], 0, 4), Math.Clamp(p[1], 0, 3)], KeepsQuantifierTwo: true) },
        { "wf_cnd_team_has_score", new(p => [Math.Clamp(p[0], 0, 4), Math.Clamp(p[1], 0, 1000), Math.Max(p[2], 0)], KeepsQuantifierTwo: true) },
        // The time conditions keep any threshold, negative ones too.
        { "wf_cnd_time_less_than", new(Unchanged, KeepsQuantifierTwo: true) },
        { "wf_cnd_time_more_than", new(Unchanged, KeepsQuantifierTwo: true) },
        { "wf_cnd_trggrer_on_frn", new(Unchanged, KeepsQuantifierTwo: true) },
        // The group conditions keep the id text only when it is a decimal number.
        { "wf_cnd_actor_in_group", new(Unchanged, DecimalOrEmpty, KeepsQuantifierTwo: true) },
        { "wf_cnd_not_in_group", new(Unchanged, DecimalOrEmpty, KeepsQuantifierTwo: true) },
        // Triggerer Matches: the user type is 1 to 4 (0 and below read as 1, above 4 as 4); the name has no length limit and a tab or a
        // line feed in it is stored as a space.
        { "wf_cnd_triggerer_match", new(TriggererType, SingleLine, KeepsQuantifierTwo: true) },
        { "wf_cnd_not_triggerer_match", new(TriggererType, SingleLine, KeepsQuantifierTwo: true) },
        { "wf_cnd_stuff_is", new(Unchanged, FitsToFrame: false, KeepsQuantifierTwo: true) },
        { "wf_cnd_not_stuff_is", new(Unchanged, FitsToFrame: false, KeepsQuantifierTwo: true) },
        { "wf_cnd_has_handitem", new(Unchanged, KeepsQuantifierTwo: true) },
        { "wf_cnd_not_has_handitem", new(Unchanged, KeepsQuantifierTwo: true) }
    }.ToFrozenDictionary();

    public static bool Applies(string name) => Rules.ContainsKey(name);

    public static bool KeepsQuantifierTwo(string name) => Rules.TryGetValue(name, out var rule) && rule.KeepsQuantifierTwo;

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        if (!Rules.TryGetValue(name, out var rule)) {
            return native;
        }

        var ints = rule.Ints(rule.FitsToFrame ? WiredStoreDraft.Fit(native.OwnedIntParams, defaults) : native.OwnedIntParams);
        var text = rule.Text is { } normalizeText ? normalizeText(native.Text) : native.Text;

        return native with { OwnedIntParams = ints, Text = text, Quantifier = StoredQuantifier(rule, native.Quantifier) };
    }

    private static int? StoredQuantifier(Rule rule, int? quantifier) => rule.KeepsQuantifierTwo && quantifier is { } value
        ? value is 1 or 2 ? value : 0 : quantifier;

    private static ImmutableArray<int> Unchanged(ImmutableArray<int> p) => p;

    private static ImmutableArray<int> Team(ImmutableArray<int> p) => [Math.Clamp(p[0], 0, 4)];

    private static ImmutableArray<int> OnOff(ImmutableArray<int> p) => [p[0] == 0 ? 0 : 1];

    private static ImmutableArray<int> OnlyOne(ImmutableArray<int> p) => [.. p.Select(flag => flag == 1 ? 1 : 0)];

    private static ImmutableArray<int> TriggererType(ImmutableArray<int> p) => [Math.Clamp(p[0], 1, 4)];

    private static string SingleLine(string text) => text.Replace('\t', ' ').Replace('\n', ' ');

    private static ImmutableArray<int> UserCount(ImmutableArray<int> p)
    {
        var minimum = Math.Clamp(p[0], 0, 125);

        return [minimum, Math.Max(Math.Clamp(p[1], 0, 125), minimum)];
    }

    // The first flag of each box switches a field on: any value but 0 is 1. Ranges keep the order they were sent in, except that a
    // year range that ends before it starts has its end raised to the start (9999..0 is stored 9999..9999).
    private static ImmutableArray<int> MatchDate(ImmutableArray<int> p)
    {
        var yearStart = Math.Clamp(p[6], 0, 9999);

        return [Flag(p[0]), Flag(p[1]), Math.Clamp(p[2], 0, 127), Math.Clamp(p[3], 1, 31), Math.Clamp(p[4], 1, 31),
            Math.Clamp(p[5], 0, 4095), yearStart, Math.Max(Math.Clamp(p[7], 0, 9999), yearStart)];
    }

    private static ImmutableArray<int> MatchTime(ImmutableArray<int> p) =>
        [Flag(p[0]), Flag(p[1]), Flag(p[2]), .. p.Skip(3).Take(4).Select(value => Math.Clamp(value, 0, 59)),
            .. p.Skip(7).Take(2).Select(value => Math.Clamp(value, 0, 23))];

    private static string ZoneOrEmpty(string text) => Modern.Conditions.WiredTimeConditions.IsValidZone(text) ? text : "";

    private static string DecimalOrEmpty(string text) => IsDecimalNumber(text) ? text : "";

    /// <summary>An optional minus sign and digits that fit a 64-bit number.</summary>
    internal static bool IsDecimalNumber(string text) => text.Length == 0
        || long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _)
        || text[0] == '-' && long.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _);

    private static int Flag(int value) => value == 0 ? 0 : 1;
}
