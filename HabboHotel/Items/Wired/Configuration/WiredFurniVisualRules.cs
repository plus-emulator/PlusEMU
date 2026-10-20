using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// What the hotel does with a save of Set Furni Highlight and Set Furni Visibility. Observed on AIR WIN63-202610201032-107964540:
/// missing ints take the frame default and extra ints are dropped, and the flags and the easing are clamped instead of refused.
/// </summary>
internal static class WiredFurniVisualRules
{
    public const string Highlight = "wf_act_set_highlight";
    public const string Visibility = "wf_act_set_visibility";

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
}
