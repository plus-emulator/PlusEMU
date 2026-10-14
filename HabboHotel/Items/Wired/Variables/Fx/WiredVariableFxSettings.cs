using System.Collections.Immutable;
using System.Globalization;
using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Variables.Fx;

/// <summary>
/// The AIR variable FX form: source (0 furni, 1 user), visibility, show mode and duration, then style/colour/width/renderer from the
/// shared catalog, the value range, range-override and audience variables, segments, then two trailing ints the AIR editor always
/// writes (observed on hhus, WIN63-202610131318, all six cards): [21] the category slot (levelling sub-renderer, number icon alignment,
/// 0 for the others) and [22] the "Order in FX stack" (any int, higher places the FX lower in the holder's stack). A number display's
/// icon rides the text. Variable ids are [range minimum, range maximum, audience].
/// </summary>
public static class WiredVariableFxSettings
{
    /// <summary>Total ints of every variable FX form (the observed official editor frame).</summary>
    public const int IntCount = 23;
    private const int CategorySlot = 21;
    private const int StackOrder = 22;

    /// <summary>
    /// The icons of the AIR editor's dropdown (VariableFxVisualizationSettingsPreset STANDARD_ICON_IDS plus the campaign "ranch." icons the
    /// editor shows behind a client flag); the renderer throws on any other asset name, so no other name is ever stored.
    /// </summary>
    private static readonly HashSet<string> Icons = ["battery", "burning", "cash", "cooldown", "droplet", "energy", "eye", "fish", "food", "freezing", "gems",
        "gold", "health", "honor", "magic", "mana", "poison", "repairing", "reputation", "shield", "stamina", "star_power", "stealth", "timeleft",
        "upgrading", "wooden_logs", "ranch.aubergine", "ranch.carrot", "ranch.corn", "ranch.egg", "ranch.grape", "ranch.potato", "ranch.pumpkin",
        "ranch.sapling", "ranch.tomato", "ranch.wheat"];

    // The delegated colours the client's baked number renderer matches by exact RGB against its RED/GREEN/BLUE/YELLOW rows
    // (VariableFxColorType.resolve); any other value paints a baked number white.
    private static string? TeamColor(int team) => team switch { 1 => "#df291e", 2 => "#36b24a", 3 => "#3b7de3", 4 => "#ffd83d", _ => null };

    public static bool IsKnownIcon(string icon) => Icons.Contains(icon);
    internal static string? TeamColorOf(int team) => TeamColor(team);

    public static int Category(string name) => name switch
    {
        "wf_xtra_var_fx_health" => 0,
        "wf_xtra_var_fx_progress" => 1,
        "wf_xtra_var_fx_level" => 2,
        "wf_xtra_var_fx_status" => 3,
        "wf_xtra_var_fx_boss" => 4,
        "wf_xtra_var_fx_number" => 5,
        _ => -1
    };

    /// <summary>The form's own defaults for a category: user source, its first style and that style's default options.</summary>
    public static ImmutableArray<int> Defaults(int category)
    {
        var style = WiredVariableFxStyles.All[(category, 0)];
        int[] ints = [1, 2, 0, 7, 0, 3000, 0, style.DefaultColor, style.DefaultWidth, style.DefaultRenderer, 0, 0, 0, 100, 0, 0, 1, 1, 0, 0, 0];

        return [.. ints, category == 2 ? style.SubRenderers[0] : 0, 0];
    }

    /// <summary>
    /// The hotel's stored form of a saved variable FX form: a colour, width or renderer id that the selected style does not offer is
    /// replaced by that style's default (-1 survives only where it is itself an option), and levelling progress keeps no value range.
    /// Forms with an unknown style or the wrong shape are returned untouched for the validator to refuse.
    /// </summary>
    public static ImmutableArray<int> Normalize(string name, ImmutableArray<int> ints)
    {
        var category = Category(name);

        if (category < 0) {
            return ints;
        }

        ints = Upgrade(category, ints);

        if (ints.Length != IntCount || !WiredVariableFxStyles.TryGet(category, ints[6], out var style)) {
            return ints;
        }

        var builder = ints.ToBuilder();
        builder[7] = style.Colors.Contains(ints[7]) ? ints[7] : style.DefaultColor;
        builder[8] = style.Widths.Contains(ints[8]) ? ints[8] : style.DefaultWidth;
        builder[9] = style.Renderers.Contains(ints[9]) ? ints[9] : style.DefaultRenderer;

        if (category == 2) {
            builder[10] = builder[11] = builder[12] = builder[13] = -1;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// A form saved before the stack order existed (21 ints, 22 for levelling and number displays) keeps its settings and takes order 0;
    /// the trailing category slot is added where the older shape had none.
    /// </summary>
    public static ImmutableArray<int> Upgrade(int category, ImmutableArray<int> ints) => ints.Length switch
    {
        21 when category is not (2 or 5) => [.. ints, 0, 0],
        22 when category is 2 or 5 => [.. ints, 0],
        _ => ints
    };

    /// <summary>The ints an editor may send: the current 23, or the shape of a form saved before the stack order.</summary>
    public static bool IsAcceptedLength(string name, int length) => Category(name) is var category and >= 0
        && (length == IntCount || length == (category is 2 or 5 ? 22 : 21));

    public static bool UsesUserSource(WiredConfiguration configuration) => configuration.IntParams.Length > 0 && configuration.IntParams[0] == 1;

    public static bool TryDecode(string name, int itemId, WiredConfiguration configuration, WiredVariableReference shownVariable,
        Func<string, WiredVariableTarget, WiredVariableReference?>? resolve, out WiredVariableFxBinding? binding, out string error)
    {
        binding = null;
        error = "Invalid variable FX settings.";
        var category = Category(name);
        var p = configuration.IntParams;

        if (category < 0 || itemId <= 0 || p.Length != IntCount || p[0] is not (0 or 1) || configuration.VariableIds.Length != 3) {
            return false;
        }

        var user = p[0] == 1;
        var target = user ? WiredVariableTarget.User : WiredVariableTarget.Furni;
        var visibility = p[1];
        var min = ((long)p[10] << 32) | (uint)p[11];
        var max = ((long)p[12] << 32) | (uint)p[13];
        var audienceValue = ((long)p[18] << 32) | (uint)p[19];

        if (shownVariable.Target != target || visibility is < 0 or > 4 || !user && visibility < 2 || p[2] is < 0 or > 2 || p[3] is < 0 or > 15
            || p[4] is not (0 or 1) || p[5] is < 1500 or > 20000 || !WiredVariableFxStyles.TryGet(category, p[6], out var style)) {
            return false;
        }

        // Number displays always span 0..100 and levelling progress stores no range (-1/-1, read as 0..100); the others need a real range.
        var unranged = category == 2 && min == -1 && max == -1;
        var range = category switch { 2 => unranged || min == 0 && max == 100, 5 => min == 0 && max == 100, _ => max > min };

        if (unranged) {
            (min, max) = (0, 100);
        }

        if (!style.Colors.Contains(p[7]) || !style.Widths.Contains(p[8]) || !style.Renderers.Contains(p[9]) || !range
            || p[14] is not (0 or 1) || p[15] is not (0 or 1) || p[16] != -10 && p[16] != p[0] || p[17] != -10 && p[17] != p[0]
            || audienceValue is < int.MinValue or > int.MaxValue || p[20] is < 0 or > 100) {
            return false;
        }

        var subRenderer = category == 2 ? p[CategorySlot] : 0;
        var segmentRenderer = category == 2 && p[9] == 20 ? subRenderer : p[9];

        if (p[20] > 0 && segmentRenderer is not (2 or 4 or 13) || category == 2 && !style.SubRenderers.Contains(subRenderer)
            || category == 5 && p[CategorySlot] is < 0 or > 2 || category is not (2 or 5) && p[CategorySlot] != 0 || category != 5 && configuration.Text.Length != 0
            || category == 5 && configuration.Text.Length != 0 && !Icons.Contains(configuration.Text)) {
            return false;
        }

        var ids = configuration.VariableIds;
        bool Present(int index) => !WiredVariableAbsent.Is(ids[index]);

        if (p[14] == 1 && !Present(0) || p[15] == 1 && !Present(1) || visibility >= 3 != Present(2) || visibility < 3 && audienceValue != 0) {
            return false;
        }

        WiredVariableReference? Reference(int index, WiredVariableTarget expected)
        {
            if (!Present(index)) {
                return null;
            }

            if (resolve is not null) {
                return resolve(ids[index], expected);
            }

            return WiredVariableDescription.TryParseCatalogId(ids[index], out var parsed, out var token) && parsed == expected ? new(parsed, token) : null;
        }

        var minimum = p[14] == 1 ? Reference(0, p[16] == -10 ? WiredVariableTarget.Global : target) : null;
        var maximum = p[15] == 1 ? Reference(1, p[17] == -10 ? WiredVariableTarget.Global : target) : null;
        var audience = visibility >= 3 ? Reference(2, WiredVariableTarget.User) : null;

        if (p[14] == 1 && minimum is null || p[15] == 1 && maximum is null || visibility >= 3 && audience is null) {
            return false;
        }

        var extra = style.Extra.ToBuilder();

        if (p[20] > 0) {
            extra["segments"] = p[20].ToString(CultureInfo.InvariantCulture);
        }

        if (category == 2) {
            extra["sub_renderer"] = subRenderer.ToString(CultureInfo.InvariantCulture);
        }
        else if (category == 5) {
            if (configuration.Text.Length > 0) {
                extra["icon"] = configuration.Text;
            }

            extra["icon_alignment"] = p[CategorySlot] switch { 1 => "right", 2 => "double", _ => "left" };
        }

        // The renderer reads VariableFxConfigData.stackOrder from the config extra, 0 when absent.
        if (p[StackOrder] != 0) {
            extra["stack_order"] = p[StackOrder].ToString(CultureInfo.InvariantCulture);
        }

        var config = new WiredVariableFxConfig(itemId, user, p[2], p[5], category, p[6], p[7], p[8], p[9], min, max, extra.ToImmutable())
        {
            UpdateMask = p[3],
            ShowOnMouseHover = p[4] == 1
        };
        binding = new(config, shownVariable, visibility, audience, (int)audienceValue, minimum, maximum);
        error = "";

        return true;
    }
}
