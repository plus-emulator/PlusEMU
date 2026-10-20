using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// What the hotel does with a save of Set Furni Highlight and Set Furni Visibility. Observed on AIR WIN63-202610201032-107964540:
/// missing ints take the frame default and extra ints are dropped, and the flags and the easing are clamped instead of refused.
/// Set Furni Highlight refuses a type, strength, colour or animation the editor does not offer, each with its own key.
/// </summary>
internal static class WiredFurniVisualRules
{
    public const string Highlight = "wf_act_set_highlight";
    public const string Visibility = "wf_act_set_visibility";
    public const string HighlightKeyPrefix = "wiredfurni.error.set_furni_highlight.";

    private const int Yellow = 0xFFEA00;
    private const int White = 0xFFFFFF;

    // The 21 colours of the editor.
    private static readonly FrozenSet<int> Palette = new[]
    {
        16777215, 16772910, 16758528, 16747008, 16734774, 16724016, 16724855, 16733627, 15741144, 12076287, 7883007,
        4611327, 2395391, 2146303, 2156016, 1628592, 2149480, 5300272, 10546464, 16557530, 12291417
    }.ToFrozenSet();

    // The strengths of each type: Outline, Soft glow, Darken, Color tint, Brighten and Neon aura.
    private static readonly int[][] Strengths = [[0, 100], [0, 100], [0], [0, 25, 50, 75, 100], [0, 38], [0, 100]];

    private const int ColourTint = 3;
    private const int Brighten = 4;

    public static bool Applies(string name) => name is Highlight or Visibility;

    public static WiredNativeEditorConfiguration Normalize(string name, WiredNativeEditorConfiguration native, ImmutableArray<int> defaults)
    {
        var ints = ImmutableArray.CreateBuilder<int>(defaults.Length);

        for (var index = 0; index < defaults.Length; index++) {
            ints.Add(index < native.OwnedIntParams.Length ? native.OwnedIntParams[index] : defaults[index]);
        }

        if (name == Highlight) {
            // [audience, type, strength, colour, animation, easing, target users]
            ints[0] = Flag(ints[0]);
            ints[4] = ints[4] > 0 ? 1 : 0;
            ints[5] = Math.Clamp(ints[5], 0, 4);
            ints[6] = Flag(ints[6]);
        }
        else {
            // [audience, visibility, click through, easing, target users]
            ints[0] = Flag(ints[0]);
            ints[2] = Flag(ints[2]);
            ints[3] = Math.Clamp(ints[3], 0, 4);
            ints[4] = Flag(ints[4]);
        }

        return native with { OwnedIntParams = ints.MoveToImmutable() };
    }

    // Any value but 0 is 1.
    private static int Flag(int value) => value == 0 ? 0 : 1;

    /// <summary>The key of the first thing wrong with a normalised Set Furni Highlight save: type, strength, pulse, then colour.</summary>
    public static string? HighlightRefusal(ImmutableArray<int> ints)
    {
        var (type, strength, color, animation) = (ints[1], ints[2], ints[3], ints[4]);

        if (type is < 0 or > 5) {
            return HighlightKeyPrefix + "invalid_type";
        }

        if (!Strengths[type].Contains(strength)) {
            return HighlightKeyPrefix + "invalid_strength";
        }

        if (type == Brighten && animation == 0) {
            return HighlightKeyPrefix + "pulse_only";
        }

        return AcceptsColor(type, color) ? null : HighlightKeyPrefix + "invalid_color";
    }

    // The editor shows no colour for Darken and Brighten; they accept the palette and black. A colour tint is never white.
    private static bool AcceptsColor(int type, int color) => color == Yellow || type switch
    {
        ColourTint => color != White && Palette.Contains(color),
        2 or Brighten => color == 0 || Palette.Contains(color),
        _ => Palette.Contains(color)
    };

    /// <summary>A steady colour tint of furni picked one by one is refused when one of them is not from the Builders Club.</summary>
    public static string? PickRefusal(string name, WiredConfiguration saved, Func<uint, bool> isBuildersClub) =>
        name == Highlight && saved.IntParams is [_, ColourTint, _, _, 0, _, 0] && saved.FurniSources.GetValueOrDefault("items") == WiredSources.Selected
            && saved.SelectedItems.Any(id => !isBuildersClub(id))
            ? HighlightKeyPrefix + "normal_furni" : null;
}
