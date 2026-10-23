using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Chests;

public class WiredChestBox : WiredModernBox
{
    protected readonly WiredChestRoom Chests;
    private static readonly ConditionalWeakTable<WiredExecutionPolicy, ContractSource> Custom = new();
    public static readonly string[] Names = ["wf_act_give_currency", "wf_act_give_furni", "wf_act_init_transaction", "wf_act_cancel_transaction",
        "wf_cnd_chest_has_items", "wf_cnd_chest_has_item_type", "wf_trg_transaction_complete", "wf_trg_transaction_fail",
        "wf_xtra_custom_contract", "wf_xtra_scan_chest_furni_by_type"];

    public WiredChestBox(Room room, Item item, WiredBoxDescriptor descriptor, WiredChestRoom chests) : base(room, item, descriptor)
    {
        Chests = chests;
        ApplyConfiguration(WiredNativeEditorProjection.DefaultRuntime(item.Id, descriptor)
            ?? throw new InvalidDataException("Chest boxes need their native defaults."));
    }
    public static IWiredConfiguredItem Create(Room room, Item item, WiredBoxDescriptor descriptor, WiredChestRoom chests) => descriptor.Category switch
    {
        WiredBoxCategory.Action => new WiredChestAction(room, item, descriptor, chests),
        WiredBoxCategory.Addon => new WiredChestAddon(room, item, descriptor, chests),
        WiredBoxCategory.Trigger => new WiredChestTrigger(room, item, descriptor, chests),
        _ => new WiredChestBox(room, item, descriptor, chests)
    };
    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Invalid chest Wired configuration.";

        if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)
            || !WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out _, out error)) {
            return false;
        }

        error = "Invalid chest Wired configuration.";
        var p = proposed.IntParams;
        bool B(int i) => p[i] is 0 or 1;
        bool T(int i) => p[i] is >= 0 and <= 3;

        return Descriptor.CanonicalName switch
        {
            "wf_act_give_currency" or "wf_act_give_furni" => p.Length == 6 && B(0) && p[1] >= 1 && B(2) && T(3) && B(4)
                && (Descriptor.CanonicalName == "wf_act_give_currency" ? p[5] is 11 or 13 : p[5] is >= 0 and <= 2),
            "wf_act_init_transaction" => p.Length == 6 && p[0] is >= 0 and <= 2 && p[1] is >= 1 and <= 500 && B(2) && T(3) && B(4) && p[5] is >= 30 and <= 3600,
            "wf_act_cancel_transaction" or "wf_xtra_scan_chest_furni_by_type" => p.Length == 1 && B(0),
            "wf_cnd_chest_has_items" or "wf_cnd_chest_has_item_type" => p.Length == 4 && p[0] is >= 0 and <= 1000000 && B(1) && T(2) && p[3] is >= 0 and <= 5,
            "wf_trg_transaction_complete" or "wf_trg_transaction_fail" => p.Length == 0,
            "wf_xtra_custom_contract" => p.Length == 10 && B(0) && B(1) && B(2) && p[3] is >= 1 and <= 100000 && T(4)
                && B(5) && B(6) && B(7) && p[8] is >= 1 and <= 100000 && T(9),
            _ => false
        } && proposed.Text.Length <= 200 && GiveChestsAccepted(proposed, ref error);
    }

    /// <summary>
    /// The pick rule of a new save that a stored row is not held to. Chest Has Item Type
    /// accepts only a Furni Chest and answers a Credit Chest, a mix of both or any other furni with the furni chest key. The chest
    /// scanner follows the same rule. A second-list pick that names no furni is dropped on a contract card, not refused.
    /// </summary>
    internal bool TryValidateSavedPicks(WiredConfiguration proposed, out string error)
    {
        error = "";

        if (PickRules.TryGetValue(Descriptor.CanonicalName, out var rule)
            && proposed.SelectedItems.Select(id => Instance.GetRoomItemHandler().GetItem(id))
                .Concat(proposed.SecondarySelectedItems.Select(id => Instance.GetRoomItemHandler().GetItem(id)).Where(item => item != null))
                .Any(item => item is null || !rule.Accepts(item))) {
            error = rule.Key;

            return false;
        }

        // Initiate Transaction picks its chests from the first pick list, and a save with no
        // chest there is refused with the chest key.
        if (Descriptor.CanonicalName == "wf_act_init_transaction" && proposed.FurniSources.GetValueOrDefault("f0", 100) == 100
            && !proposed.SelectedItems.Any(id => Instance.GetRoomItemHandler().GetItem(id) is { } item && Chests.Read(item) is not null)) {
            error = "wiredfurni.error.require_chests";

            return false;
        }

        if (Descriptor.CanonicalName is not ("wf_cnd_chest_has_item_type" or "wf_xtra_scan_chest_furni_by_type")) {
            return true;
        }

        var source = proposed.FurniSources.GetValueOrDefault("f1", 100);

        if (source is 100 or 101 && (source == 101 ? proposed.SecondarySelectedItems : proposed.SelectedItems)
                .Any(id => Instance.GetRoomItemHandler().GetItem(id) is not { } item || Chests.Read(item) is not { Kind: WiredChestKind.Furni })) {
            error = "wiredfurni.error.require_furni_chests";

            return false;
        }

        return true;
    }

    private static bool IsTransactionSource(Item item) => WiredChestFurniture.IsContract(item.Definition) || item.Definition.ItemName == "wf_xtra_custom_contract";

    private sealed record PickRule(Func<Item, bool> Accepts, string Key);

    private static readonly PickRule ContractsOrTradeWired = new(IsTransactionSource, "wiredfurni.error.require_contracts_or_trade_wired");

    // These boxes accept the three contracts and the Custom Contract add-on, and refuse a
    // pick of any other furni, the Initiate Transaction box included, from either pick list, with the key.
    private static readonly FrozenDictionary<string, PickRule> PickRules = new Dictionary<string, PickRule>
    {
        { "wf_act_cancel_transaction", new(IsTransactionSource, "wiredfurni.error.require_contracts") },
        { "wf_trg_transaction_complete", ContractsOrTradeWired },
        { "wf_trg_transaction_fail", ContractsOrTradeWired }
    }.ToFrozenDictionary();

    // give_currency and give_furni refuse a picked chest that is not
    // an upgraded Wired chest of their kind ("wiredfurni.error.require_coin_chests" / "require_furni_chests"); after the free Wired
    // upgrade the same saves are accepted. The chest conditions and the scan add-on accept normal chests.
    private bool GiveChestsAccepted(WiredConfiguration proposed, ref string error)
    {
        // Chest Has Items with a picked furni that is not a chest is
        // refused with "wiredfurni.error.require_chests"; any chest, upgraded or not, is accepted.
        if (Descriptor.CanonicalName is "wf_cnd_chest_has_items" or "wf_cnd_chest_has_item_type") {
            var slot = Descriptor.CanonicalName == "wf_cnd_chest_has_items" ? "f0" : "f1";
            var chestSource = proposed.FurniSources.GetValueOrDefault(slot, 100);

            if (chestSource is 100 or 101 && (chestSource == 101 ? proposed.SecondarySelectedItems : proposed.SelectedItems)
                    .Any(id => Instance.GetRoomItemHandler().GetItem(id) is not { } item || Chests.Read(item) is null)) {
                error = "wiredfurni.error.require_chests";

                return false;
            }

            return true;
        }

        if (Descriptor.CanonicalName is not ("wf_act_give_currency" or "wf_act_give_furni")) {
            return true;
        }

        var source = proposed.FurniSources.GetValueOrDefault("f0", 100);

        if (source is not (100 or 101)) {
            return true;
        }

        var coins = Descriptor.CanonicalName == "wf_act_give_currency";
        var kind = coins ? WiredChestKind.Coins : WiredChestKind.Furni;

        foreach (var id in source == 101 ? proposed.SecondarySelectedItems : proposed.SelectedItems) {
            if (Instance.GetRoomItemHandler().GetItem(id) is not { } item || Chests.Read(item) is not { } chest
                || chest.Kind != kind || !chest.Settings.WiredEnabled) {
                error = coins ? "wiredfurni.error.require_coin_chests" : "wiredfurni.error.require_furni_chests";

                return false;
            }
        }

        return true;
    }

    /// <summary>The furniture a furni selection resolves to: the picked group its source names, or the live/selector set.</summary>
    protected Item[] Furni(WiredRuntimeContext context, WiredConfiguration config, int slot)
    {
        var source = config.FurniSources.GetValueOrDefault("f" + slot, 100);

        return context.Targets.ResolveFurni(context, source == 101 ? config.SecondarySelectedItems : config.SelectedItems, source);
    }
    protected RoomUser[] Users(WiredRuntimeContext context, WiredConfiguration config, int slot = 0)
    {
        var source = config.UserSources.GetValueOrDefault("u" + slot, 0);

        return context.Targets.ResolveUsers(context, [], source).Where(user => !user.IsBot && user.GetClient() != null).ToArray();
    }

    /// <summary>
    /// The variable frame of this run. Only variable boxes made one before, so a chest box in a stack without them found none and its variable
    /// operands, scan results and contract amounts silently did nothing; like the variable boxes, it refreshes the frame it was given.
    /// </summary>
    protected static WiredVariableFrame Frame(WiredRuntimeContext context) =>
        context.VariableFrame = WiredVariableRuntimeFrames.Create(context, context.VariableFrame);

    /// <summary>The picked variable's current local reference, resolved through the room's catalog authority at use.</summary>
    protected WiredVariableReference? Variable(WiredRuntimeContext context, WiredConfiguration config, int slot, WiredVariableTarget target) =>
        slot < config.VariableIds.Length && !WiredVariableAbsent.Is(config.VariableIds[slot])
        && context.Room.GetWired().Variables.Module.TryResolveCatalogId(config.VariableIds[slot], target, out var reference) ? reference : null;
    protected long? Operand(WiredRuntimeContext context, WiredConfiguration config, int valueIndex, int variableIndex, int targetIndex, int variableSlot, int furniSlot, int userSlot)
    {
        if (config.IntParams[variableIndex] == 0) {
            return config.IntParams[valueIndex];
        }

        var target = (WiredVariableTarget)config.IntParams[targetIndex];

        if (Variable(context, config, variableSlot, target) is not { } reference) {
            return null;
        }

        var frame = Frame(context);

        var holders = target switch
        {
            WiredVariableTarget.Furni => Furni(context, config, furniSlot).Select(WiredVariableRuntimeFrames.FurniHolder),
            WiredVariableTarget.User => Users(context, config, userSlot).Select(WiredVariableRuntimeFrames.UserHolder),
            _ => [new WiredVariableHolder(target, 0, 0)]
        };

        foreach (var holder in holders) {
            var value = context.Room.GetWired().Variables.Module.Read(reference, holder, frame);

            if (value != null) {
                return value.Value;
            }
        }

        return null;
    }
    public override bool Execute(WiredRuntimeContext context)
    {
        var c = context.ConfigurationOf(this);
        var types = Descriptor.CanonicalName == "wf_cnd_chest_has_item_type" ? Furni(context, c, 0).Select(WiredChestItemType.Of).ToHashSet() : null;
        var slot = types == null ? 0 : 1;
        var chests = Chests.Read(Furni(context, c, slot));
        var reference = Operand(context, c, 0, 1, 2, 0, slot + 1, 0);
        var amount = types == null ? chests.Sum(chest => (long)chest.Amount)
            : chests.Sum(chest => (long)chest.Items.Count(item => types.Contains(WiredChestItemType.Of(item))));

        // With no chest to check, Chest Has Items passes whatever the comparison.
        // Chest Has Item Type with no chest counts 0 and compares.
        if (chests.Length == 0 && types == null) {
            return true;
        }

        return reference is { } value && c.IntParams[3] switch
        {
            0 => amount < value,
            1 => amount == value,
            2 => amount > value,
            3 => amount <= value,
            4 => amount != value,
            5 => amount >= value,
            _ => false
        };
    }
    // The add-on's contract is built when the transaction starts, from the variables as they are then.
    private sealed record ContractSource(Func<WiredChestContract?> Build);
    protected static void SetCustom(WiredRuntimeContext context, Func<WiredChestContract?> build)
    {
        Custom.Remove(context.Policy);
        Custom.Add(context.Policy, new(build));
    }
    protected static bool TryCustom(WiredRuntimeContext context, out WiredChestContract? contract)
    {
        var found = Custom.TryGetValue(context.Policy, out var value);
        contract = value?.Build();

        return found;
    }
}

public sealed class WiredChestAction(Room room, Item item, WiredBoxDescriptor descriptor, WiredChestRoom chests)
    : WiredChestBox(room, item, descriptor, chests), IWiredContextualAction
{
    public bool IsNegative => false;
    public override bool Execute(WiredRuntimeContext context)
    {
        var c = context.ConfigurationOf(this);
        var actors = Users(context, c);
        var picked = Furni(context, c, 0);

        if (Descriptor.CanonicalName == "wf_act_cancel_transaction") {
            var any = c.IntParams[0] == 1;
            var contracts = picked.Where(item => WiredChestFurniture.IsContract(item.Definition)).Select(item => item.Id).ToArray();

            // Without users, or without a picked contract when not cancelling every transaction, the box has nothing to act on.
            if (actors.Length == 0 || !any && contracts.Length == 0) {
                return false;
            }

            Chests.CancelByWired(actors, contracts, any);

            // The box counts as run once it was told to cancel, whether or not a transaction stood to be cancelled.
            return true;
        }

        if (Descriptor.CanonicalName == "wf_act_init_transaction") {
            WiredChestContract? contract;
            var custom = TryCustom(context, out contract);
            // Exactly one picked contract is offered; none or several is a misconfiguration.
            var contractItems = custom ? [] : Furni(context, c, 1).Where(item => WiredChestFurniture.IsContract(item.Definition)).DistinctBy(item => item.Id).Take(2).ToArray();
            var contractItem = contractItems.Length == 1 ? contractItems[0] : null;
            contract ??= contractItem == null ? null : Chests.Store.LoadContract(contractItem);
            var multiplier = c.IntParams[0] == 0 ? 1 : Operand(context, c, 1, 2, 3, 0, 2, 1);
            var changed = false;

            foreach (var actor in actors) {
                if (contract == null || multiplier == null) {
                    Chests.Fail(actor, contractItem?.Id ?? Item.Id, WiredChestFailure.Misconfig);
                }
                else {
                    changed |= Chests.Start(actor.GetClient()!, contractItem?.Id ?? Item.Id, picked, contract, c.IntParams[0],
                        (int)Math.Clamp(multiplier.Value, 1, 500), c.IntParams[4] == 1 ? c.IntParams[5] : 0);
                }
            }

            return changed;
        }

        var kind = Descriptor.CanonicalName == "wf_act_give_currency" ? WiredChestKind.Coins : WiredChestKind.Furni;
        var snapshots = Chests.Read(picked).Where(chest => chest.Kind == kind && chest.Usable).ToArray();

        // A give from upgraded but locked (and empty) chests answers each triggering user with WiredTransactionFail reason 15
        // instead of failing silently. A give is no transaction, so its failure does not fire Transaction Failed.
        if (snapshots.Length == 0) {
            foreach (var actor in actors) {
                Chests.Fail(actor, Item.Id, WiredChestFailure.NoOrLockedChests, fireTrigger: false);
                Instance.GetWired().ReportTransactionFailure(actor, Item.Id);
            }

            return false;
        }

        // After the chest check, a user who may not trade gets reason 8,
        // with no wired.error dialog and no Transaction Failed.
        var traders = actors.Where(actor => !Chests.CannotTrade(actor.GetClient()!.GetHabbo())).ToArray();

        foreach (var actor in actors.Except(traders)) {
            Chests.Fail(actor, Item.Id, WiredChestFailure.UserCantTrade, fireTrigger: false);
        }

        var amount = c.IntParams[0] == 1 ? snapshots.Sum(chest => (long)chest.Amount) : Operand(context, c, 1, 2, 3, 0, 1, 1);

        if (snapshots.Length == 0 || amount is null or <= 0 or > int.MaxValue || c.IntParams[0] == 1 && actors.Length > 1) {
            return false;
        }

        var gave = false;

        foreach (var actor in traders) {
            var current = Chests.Read(picked).Where(chest => chest.Kind == kind && chest.Usable).ToArray();
            var boundedTake = Math.Min(amount.Value, current.Sum(chest => (long)chest.Amount));

            if (boundedTake > int.MaxValue) {
                return false;
            }

            var take = (int)boundedTake;

            if (take == 0) {
                continue;
            }

            var items = current.SelectMany(chest => chest.Items).ToArray();

            if (kind == WiredChestKind.Furni && c.IntParams[5] == 2) {
                Array.Reverse(items);
            }
            else if (kind == WiredChestKind.Furni && c.IntParams[5] == 0) {
                items = current.SelectMany(chest => chest.Items.OrderBy(item => chest.RandomKeys.GetValueOrDefault(item.Id)).ThenBy(item => item.Id)).ToArray();
            }

            var reward = kind == WiredChestKind.Coins ? new[] { new WiredChestNode(take) }
                : items.Take(take).GroupBy(WiredChestItemType.Of).Select(group => new WiredChestNode(group.Count(), group.Key)).ToArray();
            var result = Chests.Move(actor.GetClient()!, new()
            {
                RoomId = room.Id,
                UserId = actor.HabboId,
                SourceId = Item.Id,
                ChestIds = current.Select(chest => chest.Id).ToArray(),
                Wired = true,
                Reward = reward,
                Order = kind == WiredChestKind.Furni ? c.IntParams[5] : 1
            });

            if (result.Succeeded && !result.Replayed) {
                actor.GetClient()!.Send(WiredTransactionSuccessComposer.Rewarded(result, c.Text, c.IntParams[4] == 1));
                gave = true;
            }
        }

        return gave;
    }
}

public sealed class WiredChestTrigger(Room room, Item item, WiredBoxDescriptor descriptor, WiredChestRoom chests)
    : WiredChestBox(room, item, descriptor, chests), IWiredContextualTrigger
{
    public IReadOnlyCollection<WiredEventKind> Events => [Descriptor.CanonicalName == "wf_trg_transaction_complete" ? WiredEventKind.TransactionComplete : WiredEventKind.TransactionFail];
    public bool HidesChat(WiredRuntimeContext context) => false;
    public override bool Execute(WiredRuntimeContext context) => Events.Contains(context.Event.Kind);
    public bool CanTrigger(WiredRuntimeContext context)
    {
        var selected = context.ConfigurationOf(this).SelectedItems;

        // A fail trigger with no source selected does not fire for a session that wired cancelled.
        if (selected.Length == 0) {
            return !context.Event.CancelledByWired;
        }

        return selected.Contains(context.Event.SourceId) || context.Event.EventItem is { } source && selected.Contains(source.Id);
    }
}

public sealed class WiredChestAddon(Room room, Item item, WiredBoxDescriptor descriptor, WiredChestRoom chests)
    : WiredChestBox(room, item, descriptor, chests), IWiredContextualAddon
{
    public bool AfterConditions => true;
    public void Reset() { }
    public override bool Execute(WiredRuntimeContext context) => Apply(context);
    public bool Apply(WiredRuntimeContext context)
    {
        var c = context.ConfigurationOf(this);

        if (Descriptor.CanonicalName == "wf_xtra_scan_chest_furni_by_type") {
            // A stack whose scan has nowhere to go still runs its effects.
            if (Variable(context, c, 0, WiredVariableTarget.Context) is { } captured && WiredVariableModule.TryDefinitionId(captured.Token, out var id)) {
                var types = Furni(context, c, 0).Select(WiredChestItemType.Of).ToHashSet();
                var frame = Frame(context);
                var count = Chests.Read(Furni(context, c, 1)).Sum(chest => c.IntParams[0] == 1
                    ? WiredChestRoom.Preview(chest).Count(types.Contains) : chest.Items.Count(item => types.Contains(WiredChestItemType.Of(item))));
                context.Room.GetWired().Variables.Module.CaptureContextValues(new Dictionary<uint, long> { [id] = count }, frame);
            }

            return true;
        }

        WiredChestNode? Node(int side)
        {
            var offset = side * 5;

            if (c.IntParams[offset] == 0) {
                return null;
            }

            var amount = Operand(context, c, offset + 3, offset + 2, offset + 4, side, side + 2, side);
            var furni = c.IntParams[offset + 1] == 1 ? Furni(context, c, side).FirstOrDefault() : null;

            return amount is < 1 or > 100000 || amount == null || c.IntParams[offset + 1] == 1 && furni == null
                ? null : new((int)amount.Value, furni == null ? null : WiredChestItemType.Of(furni));
        }

        SetCustom(context, () =>
        {
            var payment = Node(0);
            var reward = Node(1);

            return payment == null && reward == null ? null : new WiredChestContract
            {
                Id = Item.Id,
                Kind = payment == null ? WiredContractKind.Reward : reward == null ? WiredContractKind.Payment : WiredContractKind.Trade,
                PaymentMode = 1,
                Payment = payment == null ? [] : [[payment]],
                Reward = reward == null ? null : [reward]
            };
        });

        return true;
    }
}
