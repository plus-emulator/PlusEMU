using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

internal static partial class WiredNativeAuxiliaryEditor
{
    // AIR merged variable targets: 0 furni, 1 user, -10 global, -20 context. The scalar executors use 0 user, 1 furni, 2 context, 3 global.
    internal static bool TryTarget(int air, out int scalar)
    {
        scalar = air switch { 1 => 0, 0 => 1, -20 => 2, -10 => 3, _ => -1 };

        return scalar >= 0;
    }

    internal static int AirTarget(int scalar) => scalar switch { 0 => 1, 1 => 0, 2 => -20, _ => -10 };

    /// <summary>Executors take a 32-bit literal in its slot; a wider value rides two extra ints [1, high].</summary>
    private static ImmutableArray<int> WithLiteral(int[] core, int literalIndex, int high, int low)
    {
        core[literalIndex] = low;

        return high == (low < 0 ? -1 : 0) ? [.. core] : [.. core, 1, high];
    }

    private static bool Shape(WiredNativeEditorConfiguration n, int ints, int furni, int users, int variables) =>
        n.OwnedIntParams.Length == ints && n.FurniSourceTypes.Length == furni && n.UserSourceTypes.Length == users
        && n.VariableIds.Length == variables;

    private static WiredConfiguration Draft(WiredNativeEditorConfiguration n, ImmutableArray<int> ints, string text = "") => new()
    {
        IntParams = ints,
        Text = text,
        SelectedItems = [.. n.PrimaryItems.Select(item => item.ItemId)],
        SecondarySelectedItems = [.. n.SecondaryItems.Select(item => item.ItemId)],
        VariableIds = n.VariableIds
    };

    // Volt comparison codes (0 >, 1 >=, 2 ==, 3 <=, 4 <, 5 !=) for the AIR radios (0 <, 1 =, 2 >, 3 <=, 4 !=, 5 >=).
    private static readonly int[] ScalarComparison = [4, 2, 0, 3, 5, 1];

    private static IEnumerable<KeyValuePair<string, Spec>> Variables()
    {
        // Destination: variable target, value (high, low), override-existing; one furni and one user source. The October
        // editor (GiveVariable) adds value-or-variable, the reference's target and "same holder", with the reference as a
        // second variable id ("n" in value mode). A fresh box opens on [1, 0, 0, 0, 0, -10, 0].
        // Observed on hhus (WIN63-202610131318): the editor opens with two merged source selectors, the destination and the reference,
        // both defaulting to the triggering user/furni (0) and the selector pool (200), and allows wall furniture.
        yield return new("wf_act_give_var", new(Meta(2, 2, [1, 0, 0, 0, 0, -10, 0], 2) with
        {
            FurniDefaults = [0, 200],
            UserDefaults = [0, 200],
            AllowWall = true,
            LegacySlots = 1
        }, n =>
        {
            var p = n.OwnedIntParams;

            if (Shape(n, 4, 1, 1, 1) && TryTarget(p[0], out var legacyTarget)) {
                return Draft(n, WithLiteral([legacyTarget, p[3], 0, n.UserSourceTypes[0], n.FurniSourceTypes[0]], 2, p[1], p[2]));
            }

            var referenced = Shape(n, 7, 2, 2, 2);

            if (!(Shape(n, 7, 1, 1, 2) || referenced) || !TryTarget(p[0], out var target) || !TryReference(p[4], p[5], p[6], target, out var reference)) {
                return null;
            }

            int[] core = [target, p[3], 0, n.UserSourceTypes[0], n.FurniSourceTypes[0], p[4], reference, p[4] == 1 ? p[6] : 0];

            // The reference's own sources only matter when the value is read from a variable.
            return Draft(n, WithLiteral(referenced && p[4] == 1 ? [.. core, n.UserSourceTypes[1], n.FurniSourceTypes[1]] : core, 2, p[1], p[2]));
        }));
        // Observed: no secondary pick source, picked furni (100) and the triggering user (0) by default, wall furniture allowed.
        yield return new("wf_act_remove_var", new(Meta(1, 1, [1], 1) with
        {
            FurniAllowed = [[0, 100, 200, 201]],
            UsersAllowed = [[0, 200, 201]],
            FurniDefaults = [100],
            UserDefaults = [0],
            AllowWall = true
        }, n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 1, 1, 1, 1) && TryTarget(p[0], out var target)
                ? Draft(n, [target, n.UserSourceTypes[0], n.FurniSourceTypes[0]]) : null;
        }));
        // Destination target, operator, literal-or-variable option, value (high, low), reference target; destination and reference sources.
        // The October editor (ChangeVariable) adds "same holder": the reference is read on each destination holder.
        yield return new("wf_act_change_var_val", new(Meta(2, 2, [1, 0, 0, 0, 0, -10, 0], 2) with
        {
            FurniDefaults = [0, 200],
            UserDefaults = [0, 200],
            AllowWall = true
        }, n =>
        {
            var p = n.OwnedIntParams;

            if (!(Shape(n, 6, 2, 2, 2) || Shape(n, 7, 2, 2, 2)) || !TryTarget(p[0], out var target)
                || !TryReference(p[2], p[5], p.Length == 7 ? p[6] : 0, target, out var reference)) {
                return null;
            }

            int[] core = [target, p[1], p[2], 0, reference, n.UserSourceTypes[0], n.FurniSourceTypes[0], n.UserSourceTypes[1], n.FurniSourceTypes[1]];

            return Draft(n, WithLiteral(p.Length == 7 ? [.. core, p[2] == 1 ? p[6] : 0] : core, 3, p[3], p[4]));
        }));
        yield return new("wf_cnd_has_var", new(HasVar(false), HasVarSpec));
        yield return new("wf_cnd_neg_has_var", new(HasVar(true), HasVarSpec));
        // The October editor (VariableValue) adds "same holder" as a seventh field.
        // Observed on hhus (WIN63-202610131318): opens on [1, 2, 0, 0, 0, -10, 0] with the merged destination and reference source
        // selectors on the triggering user/furni (0) and the selector pool (200), and allows wall furniture.
        yield return new("wf_cnd_var_val_match", new(Meta(2, 2, [1, 2, 0, 0, 0, -10, 0], 2) with
        {
            QuantifierType = 3,
            FurniDefaults = [0, 200],
            UserDefaults = [0, 200],
            AllowWall = true
        }, n =>
        {
            var p = n.OwnedIntParams;

            if (!(Shape(n, 6, 2, 2, 2) || Shape(n, 7, 2, 2, 2)) || !TryTarget(p[0], out var target)
                || p[1] is < 0 or > 5 || !TryReference(p[2], p[5], p.Length == 7 ? p[6] : 0, target, out var reference)) {
                return null;
            }

            int[] core = [target, ScalarComparison[p[1]], p[2], 0, reference, n.UserSourceTypes[0], n.FurniSourceTypes[0],
                n.UserSourceTypes[1], n.FurniSourceTypes[1]];

            return Draft(n, WithLiteral(p.Length == 7 ? [.. core, p[2] == 1 ? p[6] : 0, n.Quantifier ?? 0] : [.. core, n.Quantifier ?? 0],
                3, p[3], p[4]));
        }));
        // Target, comparison (< or >), creation-or-update clock, duration (high, low), time unit. The October editor
        // (VariableAge) adds value-or-variable and the duration variable's target, with that variable as a second id.
        // A fresh box opens on [1, 0, 0, 0, 0, 1, 0, -10].
        yield return new("wf_cnd_var_age_match", new(Meta(2, 2, [1, 0, 0, 0, 0, 1, 0, -10], 2) with
        {
            QuantifierType = 3,
            FurniDefaults = [0, 200],
            UserDefaults = [0, 200],
            AllowWall = true,
            LegacySlots = 1
        }, n =>
        {
            var p = n.OwnedIntParams;
            var october = Shape(n, 8, 1, 1, 2);
            var referenced = Shape(n, 8, 2, 2, 2);

            if (!(Shape(n, 6, 1, 1, 1) || october || referenced) || !TryTarget(p[0], out var target)
                || ((long)p[3] << 32 | (uint)p[4]) is < int.MinValue or > int.MaxValue || p[1] is not (0 or 2) || p[2] is not (0 or 1)) {
                return null;
            }

            int[] core = [target, p[2], p[1], p[4], p[5], n.UserSourceTypes[0], n.FurniSourceTypes[0]];

            if (!october && !referenced) {
                return Draft(n, [.. core, n.Quantifier ?? 0]);
            }

            if (!TryReference(p[6], p[7], 0, target, out var reference)) {
                return null;
            }

            // The duration variable's own sources only matter when the duration is read from a variable.
            return referenced && p[6] == 1
                ? Draft(n, [.. core, p[6], reference, n.UserSourceTypes[1], n.FurniSourceTypes[1], n.Quantifier ?? 0])
                : Draft(n, [.. core, p[6], reference, n.Quantifier ?? 0]);
        }));
        // Variable (fixed target), comparison, value mode (none, literal, variable), value (high, low), the variable's target.
        // The sixth int is WithVariable.as referenceVariableOnTarget (AIR WIN63-202610131318-860371703 selectors/WithVariable.as:48-56); hhus opens it on 0.
        // Observed: a selector offers no selector-pool source, defaults to picked furni (100) and the triggering user (0), allows walls.
        yield return new("wf_slc_furni_with_var", new(SelectorMeta([2, 0, 0, 0, -10, 0]), n => WithVariable(n, 1)));
        yield return new("wf_slc_users_with_var", new(SelectorMeta([2, 0, 0, 0, -10, 0]), n => WithVariable(n, 0)));
        // Created, value changed, deleted; increased/decreased/unchanged mask; origin mask. The target comes from the chosen variable.
        yield return new("wf_trg_var_changed", new(Meta(0, 0, [1, 1, 0, 7, -1, 0], 1), n =>
        {
            var p = n.OwnedIntParams;

            // The scalar target follows the picked variable's own target; an unchosen variable keeps the user default.
            var target = 0;

            // AIR VariableUpdate: mode 1 watches the selected variable definition boxes, independently of the disabled picker.
            if (Shape(n, 6, 0, 0, 1) && p[5] == 1) {
                if (p[0] is not (0 or 1) || p[1] is not (0 or 1) || p[2] is not (0 or 1) || p[3] is < 0 or > 7) {
                    return null;
                }

                var pickedDraft = Draft(n, [0, p[0], p[1], p[3] & 1, p[3] >> 1 & 1, p[3] >> 2 & 1, p[2], p[4] < 0 ? -1 : p[4] & 15, 1]);

                return Plus.HabboHotel.Items.Wired.Variables.WiredVariableChangedTrigger.TryNormalize(pickedDraft, out var pickedNormalized, out _) ? pickedNormalized : pickedDraft;
            }

            // Mode 0 retains the existing scalar catalogue lookup and its eight-field runtime.
            if (Shape(n, 6, 0, 0, 1) && p[5] == 0) {
                n = n with { OwnedIntParams = p[..5] };
                p = n.OwnedIntParams;
            }

            if (!Shape(n, 5, 0, 0, 1) || !Plus.HabboHotel.Items.Wired.Variables.WiredVariableAbsent.Is(n.VariableIds[0])
                && !(Plus.HabboHotel.Items.Wired.Variables.WiredVariableDescription.TryParseCatalogId(n.VariableIds[0], out var picked, out _)
                    && (target = (int)picked) >= 0)
                || p[0] is not (0 or 1) || p[1] is not (0 or 1) || p[2] is not (0 or 1) || p[3] is < 0 or > 7) {
                return null;
            }

            var draft = Draft(n, [target, p[0], p[1], p[3] & 1, p[3] >> 1 & 1, p[3] >> 2 & 1, p[2], p[4] < 0 ? -1 : p[4] & 15]);

            return Plus.HabboHotel.Items.Wired.Variables.WiredVariableChangedTrigger.TryNormalize(draft, out var normalized, out _) ? normalized : draft;
        }));
    }

    /// <summary>
    /// The October value-or-variable tail: the option (0 value, 1 variable), the reference's AIR target and "same holder",
    /// which only applies to a variable read on holders of the destination's own kind.
    /// </summary>
    private static bool TryReference(int option, int air, int sameHolder, int target, out int reference) =>
        TryTarget(air, out reference) && option is 0 or 1 && sameHolder is 0 or 1
        && (sameHolder == 0 || option == 0 || reference == target && target is 0 or 1);

    private static WiredNativeEditorMetadata SelectorMeta(ImmutableArray<int> owned) => Meta(1, 1, owned, 2) with
    {
        FurniAllowed = [[0, 100, 201]],
        UsersAllowed = [[0, 201]],
        FurniDefaults = [100],
        UserDefaults = [0],
        AllowWall = true
    };

    // Observed on AIR WIN63-202610131318-860371703: furni 0/100/200/201 opening on 100, users 0/200/201 opening on 0.
    private static WiredNativeEditorMetadata HasVar(bool negative) => Meta(1, 1, [1], 1) with
    {
        QuantifierType = 3,
        Invert = negative,
        AllowWall = true,
        FurniAllowed = [[0, 100, 200, 201]],
        FurniDefaults = [100]
    };

    private static WiredConfiguration? HasVarSpec(WiredNativeEditorConfiguration n)
    {
        var p = n.OwnedIntParams;

        return Shape(n, 1, 1, 1, 1) && TryTarget(p[0], out var target)
            ? Draft(n, [target, n.UserSourceTypes[0], n.FurniSourceTypes[0], n.Quantifier ?? 0]) : null;
    }

    private static WiredConfiguration? WithVariable(WiredNativeEditorConfiguration n, int destination)
    {
        var p = n.OwnedIntParams;

        // WithVariable.as: referenceVariableOnTarget reads the reference on each candidate holder.
        var sameHolder = Shape(n, 6, 1, 1, 2) ? p[5] : 0;

        if (sameHolder is not (0 or 1)) {
            return null;
        }

        if (Shape(n, 6, 1, 1, 2)) {
            n = n with { OwnedIntParams = p[..5] };
            p = n.OwnedIntParams;
        }

        if (!Shape(n, 5, 1, 1, 2) || n.Filter is null || n.Inverse is null || p[0] is < 0 or > 5 || p[1] is < 0 or > 2
            || !TryTarget(p[4], out var target) || sameHolder == 1 && (p[1] != 2 || target != destination) || p[3] < 0 != (p[2] == -1) || p[2] is not (0 or -1)) {
            return null;
        }

        // The selector executor takes a 32-bit literal; a wider one has no representation here.
        int[] core = [p[1] == 0 ? 0 : 1, ScalarComparison[p[0]], p[1] == 2 ? 1 : 0, p[3], target, n.UserSourceTypes[0],
            n.FurniSourceTypes[0], n.Filter.Value ? 1 : 0, n.Inverse.Value ? 1 : 0];

        return Draft(n, sameHolder == 1 ? [.. core, 1] : [.. core]);
    }
}
