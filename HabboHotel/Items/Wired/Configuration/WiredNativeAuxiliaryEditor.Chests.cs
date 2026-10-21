using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Configuration;

internal static partial class WiredNativeAuxiliaryEditor
{
    // Observed on hhus (WIN63-202610131318, r62 batch 04): a chest card with one pick list offers the triggering furni, the picks, the selector
    // and the signal; one with two pick lists (item types and chests) also offers the second list (101) on every slot. Users never include
    // the clicked user (11) here.
    private static readonly ImmutableArray<int> OnePickList = [0, 100, 200, 201];
    private static readonly ImmutableArray<int> TwoPickLists = [0, 100, 101, 200, 201];
    private static readonly ImmutableArray<int> TransactionUsers = [0, 200, 201];

    private static WiredNativeEditorMetadata TransactionMeta(ImmutableArray<int> furniSources, int furniSelections, int userSelections, ImmutableArray<int> owned, int variables,
        ImmutableArray<int> furniDefaults, ImmutableArray<int> userDefaults) => new(
            [.. Enumerable.Repeat(furniSources, furniSelections)], [.. Enumerable.Repeat(TransactionUsers, userSelections)],
            furniDefaults, userDefaults, owned, true)
        {
            VariableDefaults = [.. Enumerable.Repeat(Plus.HabboHotel.Items.Wired.Variables.WiredVariableAbsent.WireId, variables)]
        };

    /// <summary>Selections become indexed role sources ("f0", "u1", ...) the chest boxes read.</summary>
    private static WiredConfiguration ChestDraft(WiredNativeEditorConfiguration n, int[] ints, string text = "") => Draft(n, [.. ints], text) with
    {
        FurniSources = n.FurniSourceTypes.Select((source, index) => (source, index)).ToImmutableDictionary(pair => "f" + pair.index, pair => pair.source),
        UserSources = n.UserSourceTypes.Select((source, index) => (source, index)).ToImmutableDictionary(pair => "u" + pair.index, pair => pair.source)
    };

    // Transaction triggers filter the explicitly picked furniture.
    private static readonly WiredNativeEditorMetadata TransactionTrigger = new([[100]], [], [100], [], [], false);

    private static IEnumerable<KeyValuePair<string, Spec>> Chests()
    {
        // Mode (amount or all), amount, literal-or-variable option, its target, show-by-default, then currency category / iteration order.
        // Observed r93h r93-p2-chests-empty initial frames (pkt 8769): both slots offer [0, 100, 101, 200, 201] for furni and [0, 200, 201]
        // for users, defaulting to [100, 200] and [0, 200], with wall items allowed.
        yield return new("wf_act_give_currency", new(TransactionMeta(TwoPickLists, 2, 2, [0, 1, 0, -20, 1, 11], 1, [100, 200], [0, 200]), n => Reward(n, 11, 13)));
        yield return new("wf_act_give_furni", new(TransactionMeta(TwoPickLists, 2, 2, [0, 1, 0, -20, 1, 0], 1, [100, 200], [0, 200]), n => Reward(n, 0, 2)));
        // Mode, multiplier, option, target, timeout flag and seconds; chests, contracts and the merged reference.
        // Observed on hhus: mode 0 stores a multiplier below 2 as 2. The editor frames open the chests on 100, the contracts on 101,
        // the users on 0 and the merged reference on the selector (furni 200, users 200).
        yield return new("wf_act_init_transaction", new(TransactionMeta(TwoPickLists, 3, 2, [0, 10, 0, -20, 0, 300], 1, [100, 101, 200], [0, 200]), n =>
        {
            var p = n.OwnedIntParams;

            // Observed on AIR WIN63-202610201032-107964540: a raw save of a timeout from 0 to 29 seconds is accepted and read back as 30.
            return Shape(n, 6, 3, 2, 1) && TryTarget(p[3], out var target)
                ? ChestDraft(n, [p[0], p[0] == 0 && p[1] == 1 ? 2 : p[1], p[2], target, p[4], p[5] is >= 0 and < 30 ? 30 : p[5]], n.Text) : null;
        }));
        // Observed on hhus (AIR WIN63-202610131318-860371703): the only chest card without wall picks.
        yield return new("wf_act_cancel_transaction", new(TransactionMeta(OnePickList, 1, 1, [0], 0, [100], [0]) with { AllowWall = false }, n =>
            Shape(n, 1, 1, 1, 0) ? ChestDraft(n, [.. n.OwnedIntParams]) : null));
        // Compared amount, its option and target, comparison (the form's own codes).
        // Observed on AIR WIN63-202610201032-107964540: both conditions open with the quantifier selector (quantifier type 1).
        yield return new("wf_cnd_chest_has_items", new(TransactionMeta(OnePickList, 2, 1, [0, 0, -20, 2], 1, [100, 200], [200]) with { QuantifierType = 1 }, HasChest(2)));
        yield return new("wf_cnd_chest_has_item_type", new(TransactionMeta(TwoPickLists, 3, 1, [0, 0, -20, 2], 1, [100, 101, 0], [0]) with { QuantifierType = 1 }, HasChest(3)));
        yield return new("wf_trg_transaction_complete", new(TransactionTrigger, n => Shape(n, 0, 1, 0, 0) && n.FurniSourceTypes[0] == 100 ? ChestDraft(n, []) : null));
        yield return new("wf_trg_transaction_fail", new(TransactionTrigger, n => Shape(n, 0, 1, 0, 0) && n.FurniSourceTypes[0] == 100 ? ChestDraft(n, []) : null));
        // Scanning mode; item types and chests, writing into a context variable.
        yield return new("wf_xtra_scan_chest_furni_by_type", new(TransactionMeta(TwoPickLists, 2, 0, [0], 1, [0, 100], []), n =>
            Shape(n, 1, 2, 0, 1) ? ChestDraft(n, [.. n.OwnedIntParams]) : null));
        // Payment then reward: enabled, element type, option, amount, target; payment/reward furni, then the merged references.
        // The editor frames open both furni sources on the triggering item and both merged references on the selector.
        yield return new("wf_xtra_custom_contract", new(TransactionMeta(TwoPickLists, 4, 2, [0, 0, 0, 1, -20, 0, 0, 0, 1, -20], 2, [0, 0, 200, 200], [200, 200]), n =>
        {
            var p = n.OwnedIntParams;

            return Shape(n, 10, 4, 2, 2) && TryTarget(p[4], out var payment) && TryTarget(p[9], out var reward)
                ? ChestDraft(n, [p[0], p[1], p[2], p[3], payment, p[5], p[6], p[7], p[8], reward]) : null;
        }));
    }

    // Reward mode, amount, option, target, show-by-default, then the category or iteration order; the popup text rides along.
    private static WiredConfiguration? Reward(WiredNativeEditorConfiguration n, int lowest, int highest)
    {
        var p = n.OwnedIntParams;

        // Observed on AIR WIN63-202610201032-107964540: a raw save with amount 0 is accepted and read back as 1.
        return Shape(n, 6, 2, 2, 1) && TryTarget(p[3], out var target) && p[5] >= lowest && p[5] <= highest
            ? ChestDraft(n, [p[0], p[1] == 0 ? 1 : p[1], p[2], target, p[4], p[5]], n.Text) : null;
    }

    private static Func<WiredNativeEditorConfiguration, WiredConfiguration?> HasChest(int furni) => n =>
    {
        var p = n.OwnedIntParams;

        return Shape(n, 4, furni, 1, 1) && TryTarget(p[2], out var target) ? ChestDraft(n, [p[0], p[1], target, p[3]]) : null;
    };
}
