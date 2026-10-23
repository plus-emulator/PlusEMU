using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these selector saves. A save with an odd value is not refused: the value is clamped or reset, a
/// missing int takes the frame default and an extra int is dropped.
/// </summary>
internal static class WiredSelectorStoreRules
{
    private const int FilterTextLength = 200;

    // The last of the 14 mask words of the neighborhood covers 25 tiles.
    private const int LastMaskWordBits = 0x1ffffff;

    // Each selector is named once, with its stored form. The ints are cut and padded to the frame before the rule runs.
    private static readonly FrozenDictionary<string, Action<WiredStoreDraft>> Rules = new Dictionary<string, Action<WiredStoreDraft>>
    {
        // The altitude is 0..8000. The comparison keeps 3, 4 and 5, which the editor never offers, and resets any other value to 0.
        {
            "wf_slc_furni_altitude", draft =>
            {
                draft.Ints[0] = Math.Clamp(draft.Ints[0], 0, 8000);
                draft.Ints[1] = draft.Ints[1] is >= 0 and <= 5 ? draft.Ints[1] : 0;
            }
        },
        { "wf_slc_furni_area", _ => { } },
        // The state flag is on only for the exact value 1.
        { "wf_slc_furni_bytype", draft => draft.Ints[0] = draft.Ints[0] == 1 ? 1 : 0 },
        // The reference reads 0 or 1, the root offset is -64..64, the last mask word keeps its 25 tile bits and the 18th int is always 0.
        { "wf_slc_furni_neighborhood", Neighborhood },
        // Union or intersection reads 1 for any positive value and 0 otherwise; a negative stack count reads 0.
        {
            "wf_slc_remote", draft =>
            {
                draft.Ints[0] = draft.Ints[0] > 0 ? 1 : 0;
                draft.Ints[1] = Math.Max(draft.Ints[1], 0);
            }
        },
        // 1 to 4 are kept, a value below 1 reads 1 and a value above 4 reads 4.
        { "wf_slc_users_bytype", draft => draft.Ints[0] = Math.Clamp(draft.Ints[0], 1, 4) },
        // The actions the editor offers, the unoffered 9 and the client expression 67 are kept; anything else reads 0. The filter text
        // is kept as sent, up to its first 200 characters.
        { "wf_slc_users_byaction", ByAction },
        // The group id is kept as a plain number or cleared; the box has no ints.
        { "wf_slc_users_group", Group },
        { "wf_slc_users_handitem", _ => { } },
        // Team numbers run 0 (any team) to 4 (Yellow). A number above 4 reads 4 and a number below 0 reads 0.
        { "wf_slc_users_team", draft => draft.Ints[0] = Math.Clamp(draft.Ints[0], 0, 4) },
        // The four ints are fitted to the frame; the clamp to 0..64 is applied when the save is projected.
        { "wf_slc_users_area", _ => { } },
        // Same stored form as the furni neighborhood.
        { "wf_slc_users_neighborhood", Neighborhood },
        // The box has no ints and no text, and its sources are fixed.
        { "wf_slc_users_signal", _ => { } },
        // The box has no ints. The names are cut at their trailing tabs; the editor lowers their case (see WiredNativeEditorProjection).
        { "wf_slc_users_byname", draft => draft.Text = draft.Text.TrimEnd('\t') },
        // The comparison keeps 0 to 5 and reads any other value as 0. The value filter keeps 0 to 2: a larger value reads 2 and a
        // negative value reads 0. The two reference value ints are stored as sent. The reference target keeps -20, -10, 0 and 1, and
        // the same-holder flag reads 1 for any value but 0.
        { "wf_slc_users_with_var", WithVariable }
    }.ToFrozenDictionary();

    private const int MaxUsernameLength = 30;
    private const int MaxUsernames = 20;

    /// <summary>
    /// The refusal of a Users By Name save, or null. A name of more than 30 characters and more than 20 names are refused with their own key and limit.
    /// Only the tab separates names.
    /// </summary>
    public static (string Key, IReadOnlyList<KeyValuePair<string, string>> Parameters)? UsernameRefusal(string name, WiredNativeEditorConfiguration native)
    {
        if (name != "wf_slc_users_byname") {
            return null;
        }

        var names = native.Text.TrimEnd('\t').Split('\t');

        if (names.Any(entry => entry.Length > MaxUsernameLength)) {
            return ("wiredfurni.error.username_length", [new("max_username_length", MaxUsernameLength.ToString(CultureInfo.InvariantCulture))]);
        }

        return names.Length > MaxUsernames
            ? ("wiredfurni.error.usernames_count", [new("max_usernames", MaxUsernames.ToString(CultureInfo.InvariantCulture))]) : null;
    }

    public static bool Applies(string name) => Rules.ContainsKey(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        if (!Rules.TryGetValue(name, out var store)) {
            return native;
        }

        var draft = new WiredStoreDraft(native, defaults);
        store(draft);

        return draft.ToNative();
    }

    private static void Neighborhood(WiredStoreDraft draft)
    {
        var p = draft.Ints;

        p[0] = p[0] is 0 or 1 ? p[0] : 0;
        p[1] = Math.Clamp(p[1], -64, 64);
        p[2] = Math.Clamp(p[2], -64, 64);
        p[16] &= LastMaskWordBits;
        p[17] = 0;
    }

    private static void WithVariable(WiredStoreDraft draft)
    {
        var p = draft.Ints;

        p[0] = p[0] is >= 0 and <= 5 ? p[0] : 0;
        p[1] = Math.Clamp(p[1], 0, 2);
        p[4] = p[4] is -20 or -10 or 0 or 1 ? p[4] : 0;
        p[5] = p[5] == 0 ? 0 : 1;
    }

    private static void ByAction(WiredStoreDraft draft)
    {
        draft.Ints[0] = draft.Ints[0] is >= 0 and <= 11 or 67 ? draft.Ints[0] : 0;
        draft.Text = draft.Text.Length > FilterTextLength ? draft.Text[..FilterTextLength] : draft.Text;
    }

    private static void Group(WiredStoreDraft draft)
    {
        draft.Ints.Clear();
        draft.Text = long.TryParse(draft.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var group)
            ? group.ToString(CultureInfo.InvariantCulture) : "";
    }
}
