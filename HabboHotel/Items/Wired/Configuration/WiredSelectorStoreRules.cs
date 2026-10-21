using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// The normal form Habbo stores for these selector saves. A save with an odd value is not refused: the value is clamped or reset, a
/// missing int takes the frame default and an extra int is dropped.
/// Observed on AIR WIN63-202610201032-107964540.
/// </summary>
internal static class WiredSelectorStoreRules
{
    private static readonly FrozenSet<string> Boxes = new[]
    {
        "wf_slc_furni_altitude", "wf_slc_furni_area", "wf_slc_furni_bytype", "wf_slc_furni_neighborhood", "wf_slc_remote",
        "wf_slc_users_bytype", "wf_slc_users_byaction", "wf_slc_users_group", "wf_slc_users_handitem"
    }.ToFrozenSet();

    private const int FilterTextLength = 200;

    // The last of the 14 mask words of the neighborhood covers 25 tiles.
    private const int LastMaskWordBits = 0x1ffffff;

    public static bool Applies(string name) => Boxes.Contains(name);

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var p = Fit(native.OwnedIntParams, defaults).ToBuilder();
        var text = native.Text;

        switch (name) {
            // The altitude is 0..8000. The comparison keeps 3 and 4, which the editor never offers, and resets any other value to 0.
            case "wf_slc_furni_altitude":
                p[0] = Math.Clamp(p[0], 0, 8000);
                p[1] = p[1] is >= 0 and <= 4 ? p[1] : 0;
                break;
            // The state flag is on only for the exact value 1.
            case "wf_slc_furni_bytype":
                p[0] = p[0] == 1 ? 1 : 0;
                break;
            // Union or intersection reads 1 for any positive value and 0 otherwise; a negative stack count reads 0.
            case "wf_slc_remote":
                p[0] = p[0] > 0 ? 1 : 0;
                p[1] = Math.Max(p[1], 0);
                break;
            // 1 to 4 are kept, a value below 1 reads 1 and a value above 4 reads 4.
            case "wf_slc_users_bytype":
                p[0] = Math.Clamp(p[0], 1, 4);
                break;
            // The actions the editor offers, the unoffered 9 and the client expression 67 are kept; anything else reads 0. The filter text
            // is kept as sent, up to its first 200 characters.
            case "wf_slc_users_byaction":
                p[0] = p[0] is >= 0 and <= 11 or 67 ? p[0] : 0;
                text = text.Length > FilterTextLength ? text[..FilterTextLength] : text;
                break;
            // The group id is kept as a plain number or cleared; the box has no ints.
            case "wf_slc_users_group":
                p.Clear();
                text = long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var group)
                    ? group.ToString(CultureInfo.InvariantCulture) : "";
                break;
            // The reference reads 0 or 1, the root offset is -64..64, the last mask word keeps its 25 tile bits and the 18th int is always 0.
            case "wf_slc_furni_neighborhood":
                p[0] = p[0] is 0 or 1 ? p[0] : 0;
                p[1] = Math.Clamp(p[1], -64, 64);
                p[2] = Math.Clamp(p[2], -64, 64);
                p[16] &= LastMaskWordBits;
                p[17] = 0;
                break;
        }

        return native with { OwnedIntParams = p.ToImmutable(), Text = text };
    }

    private static ImmutableArray<int> Fit(ImmutableArray<int> ints, ImmutableArray<int> defaults) =>
        [.. Enumerable.Range(0, defaults.Length).Select(index => index < ints.Length ? ints[index] : defaults[index])];
}
