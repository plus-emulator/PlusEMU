using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired;

internal sealed partial class WiredStackEngine
{
    private RuntimeFiring? _activeFiring;
    private PendingDispatch? _transferringDispatch;
    private int _queuedSlots;
    private int _dispatchAdvanceDepth;
    private int PendingCount => _pending.Count + _queuedSlots - (_transferringDispatch != null
        && _dispatches.TryPeek(out var head) && ReferenceEquals(head, _transferringDispatch) ? 1 : 0);

    private void SnapshotDispatch(PendingDispatch pending)
    {
        if (pending.Triggers != null) {
            return;
        }

        if (pending.Call is { } call) {
            pending.Triggers = [];
            pending.CapturedBoxes = GetStack(call.Source);
            pending.CapturedPositions = pending.CapturedBoxes.Select(box => (box, box.Item.GetX, box.Item.GetY, box.Item.GetZ, box.Item.MovementGeneration)).ToArray();

            return;
        }

        // Observed r92g r92-signals two-w1..w3 (AIR WIN63-202610131318-860371703): two say stacks on one message ran stack two first
        // once and stack one first twice, so stacks of one event are not run in id order. They run in a per-event random order.
        var triggers = _items.Values.OfType<IWiredContextualTrigger>()
            .Where(trigger => RuntimeSupported(trigger) && trigger.Events.Contains(pending.Event.Kind))
            .OrderBy(trigger => trigger.Item.GetZ).ThenBy(trigger => trigger.Item.Id).ToArray();
        _effectOrderRandom.Shuffle(triggers);
        pending.Triggers = triggers;
        var legacyType = pending.Event.Kind switch
        {
            WiredEventKind.GameStart => WiredBoxType.TriggerGameStarts,
            WiredEventKind.GameEnd => WiredBoxType.TriggerGameEnds,
            _ => WiredBoxType.None
        };

        if (pending.IncludeLegacy && legacyType != WiredBoxType.None) {
            pending.LegacyTriggers = _items.Values.Where(box => box is not IWiredContextualTrigger && box.Type == legacyType)
                .OrderBy(box => box.Item.GetZ).ThenBy(box => box.Item.Id).ToArray();
        }

        pending.TriggerPositions = pending.Triggers.ToDictionary(trigger => trigger,
            trigger => (trigger.Item.GetX, trigger.Item.GetY, trigger.Item.GetZ, trigger.Item.MovementGeneration));
        pending.Slots = Math.Max(1, pending.Triggers.Length + pending.LegacyTriggers.Length);

        if (pending.Signal != null) {
            pending.RecipientStacks = pending.Triggers.ToDictionary(trigger => trigger, trigger => GetStack(trigger)
                .Select(box => (box, box.Item.GetX, box.Item.GetY, box.Item.GetZ, box.Item.MovementGeneration)).ToArray());
        }
        else {
            pending.CapturedBoxes = pending.Triggers.SelectMany(trigger => GetStack(trigger)).Distinct().ToArray();
            pending.CapturedPositions = pending.CapturedBoxes.Select(box => (box, box.Item.GetX, box.Item.GetY, box.Item.GetZ, box.Item.MovementGeneration)).ToArray();
        }
    }

    private void QueueDispatch(PendingDispatch pending)
    {
        pending.IsQueued = true;
        _queuedSlots += pending.Slots;
        _dispatches.Enqueue(pending);
    }

    private void CompleteDispatchSlot(PendingDispatch pending)
    {
        if (pending.Slots <= 0) {
            return;
        }

        pending.Slots--;

        if (pending.IsQueued) {
            _queuedSlots--;
        }
    }

    private void RemoveDispatchHead()
    {
        var pending = _dispatches.Dequeue();
        _queuedSlots -= pending.Slots;
        pending.IsQueued = false;
        pending.Current?.Dispose();
    }

    private bool AdvanceDispatch(PendingDispatch pending)
    {
        BeforePublicationDispatch(pending);
        _dispatchAdvanceDepth++;

        try {
            return AdvanceDispatchCore(pending);
        }
        finally {
            _dispatchAdvanceDepth--;
        }
    }

    private bool AdvanceDispatchCore(PendingDispatch pending)
    {
        var queued = pending.IsQueued;

        if (pending.Signal == null && pending.CapturedPositions.Any(position => !IsAttached(position.Box)
            || (position.Box.Item.GetX, position.Box.Item.GetY, position.Box.Item.GetZ, position.Box.Item.MovementGeneration)
                != (position.X, position.Y, position.Z, position.Generation))) {
            pending.Current?.Dispose();

            return true;
        }

        if (pending.Signal is { } signal) {
            if (signal.Receivers.Any(antenna => !_targets!.IsAttached(antenna.Receiver)
                || antenna.Receiver.MovementGeneration != antenna.Generation)) {
                pending.Current?.Dispose();

                return true;
            }
        }

        SnapshotDispatch(pending);

        if (!pending.Initialized) {
            if (pending.Event.EventItem is { } item && !_targets!.IsAttached(item)) {
                return true;
            }

            if (pending.Signal is { } delivered) {
                delivered.Context.VariableFrame = delivered.Parent.VariableFrame;
            }

            pending.Root = pending.Call is { } call ? call.Parent.Fork(pending.Event, pending.Depth)
                : pending.Signal?.Context ?? pending.Root ?? CreateContext(pending.Event, pending.Depth);

            pending.Root.InheritedPublication = pending.Publication;

            if (pending.Call is { } called) {
                pending.Root.Triggering = called.Selection.Copy();
                pending.Root.Selected = pending.Root.Triggering.Copy();
                pending.Root.ResumeImmediately = pending.ResumeImmediately;
                pending.Current = BeginFiring(called.Source, pending.Root, false, called: true);
            }

            if (!IsActorPresent(new([], pending.Depth, Runtime: pending.Root))) {
                return true;
            }

            ObserveEvent?.Invoke(pending.Event, _now());
            pending.Initialized = true;
        }

        while (true) {
            if (queued && !pending.IsQueued) {
                pending.Current?.Dispose();

                return true;
            }

            if (pending.Current != null) {
                var transfer = _dispatches.TryPeek(out var head) && ReferenceEquals(head, pending);

                if (!ResumeFiring(pending.Current, transfer)) {
                    return false;
                }

                pending.Accepted |= pending.Current.Accepted;

                if (pending.Event.Kind == WiredEventKind.ClickUser && pending.Current.Accepted && pending.Current.ConditionsPassed
                    && pending.Current.Context.Trigger is IWiredClickTrigger clickTrigger) {
                    var settings = clickTrigger.ClickSettings(pending.Current.Context);
                    pending.Click = new(true, pending.Click.BlockMenu || settings.BlockMenu, pending.Click.DoNotRotate || settings.DoNotRotate);
                }

                if (pending.Call == null && pending.Event.Kind == WiredEventKind.Speech && pending.Current.Accepted && pending.Current.ConditionsPassed
                    && pending.Current.Context.Trigger is IWiredContextualTrigger speechTrigger) {
                    pending.ConsumedChat |= speechTrigger.HidesChat(pending.Current.Context);
                }

                pending.Current.Dispose();
                pending.Current = null;
                CompleteDispatchSlot(pending);
            }

            if (pending.LegacyNext < pending.LegacyTriggers.Length) {
                if (OutOfBudget()) {
                    return false;
                }

                var box = pending.LegacyTriggers[pending.LegacyNext++];
                var previous = _legacyRuntimeEvent;
                _legacyRuntimeEvent = pending.Event;

                try {
                    Execute(box, new([], pending.Depth));
                }
                finally {
                    _legacyRuntimeEvent = previous;
                    CompleteDispatchSlot(pending);
                }

                continue;
            }

            if (pending.Next >= pending.Triggers!.Length) {
                return true;
            }

            if (OutOfBudget()) {
                return false;
            }

            var trigger = pending.Triggers[pending.Next++];

            if (!IsAttached(trigger) || pending.TriggerPositions[trigger]
                != (trigger.Item.GetX, trigger.Item.GetY, trigger.Item.GetZ, trigger.Item.MovementGeneration)
                || pending.Signal != null && (pending.CancelledRecipients.Contains(trigger)
                    || pending.RecipientStacks[trigger].Any(position => !IsAttached(position.Box)
                        || (position.Box.Item.GetX, position.Box.Item.GetY, position.Box.Item.GetZ, position.Box.Item.MovementGeneration)
                            != (position.X, position.Y, position.Z, position.Generation)))) {
                CompleteDispatchSlot(pending);
                continue;
            }

            var context = pending.Root!.Fork(pending.Event, pending.Depth);
            context.Trigger = trigger;
            context.ResumeImmediately = pending.ResumeImmediately;

            if (pending.Signal is { } forwarded) {
                context.Signal = new(forwarded.Context.Signal!.Selection, forwarded.Context.Signal.Values);
                context.Triggering = forwarded.Context.Triggering.Copy();
                context.Selected = context.Triggering.Copy();
            }
            else {
                SeedEvent(context);
            }

            context.Capture(GetStack(trigger));

            if (InvokeRuntime(trigger, context, () => context.Event.Kind == WiredEventKind.Speech
                    ? CaptureSpeech?.Invoke(context, trigger) ?? trigger.Execute(context) : trigger.Execute(context))) {
                pending.Current = BeginFiring(trigger, context, pending.Signal == null ? null : false, trigger: trigger);
            }
            else {
                CompleteDispatchSlot(pending);
            }
        }
    }

    private RuntimeFiring BeginFiring(IWiredItem source, WiredRuntimeContext context, bool? negative, object[][]? actors = null,
        IWiredContextualTrigger? trigger = null, bool called = false)
    {
        context.Trigger ??= source;
        var firing = new RuntimeFiring(source, GetStack(source), context, _now());
        context.Capture(firing.Stack);
        firing.Steps = EvaluateFiring(firing, negative, actors, trigger, called).GetEnumerator();

        return firing;
    }

    private bool ResumeFiring(RuntimeFiring firing, bool transfer = false)
    {
        if (firing.Cancelled || !IsActorPresent(new([], firing.Context.Depth, Runtime: firing.Context))
            || firing.Positions.Any(position => !IsAttached(position.Box) || position.Box.Item.MovementGeneration != position.Generation
                || (position.Box.Item.GetX, position.Box.Item.GetY, position.Box.Item.GetZ) != (position.X, position.Y, position.Z))) {
            firing.Cancelled = true;

            return true;
        }

        var previous = _activeFiring;
        _activeFiring = firing;

        try {
            while (true) {
                if (firing.Next == null) {
                    if (!firing.Steps.MoveNext()) {
                        SettleFlashes(firing);

                        return true;
                    }

                    firing.Next = firing.Steps.Current;
                }

                if (firing.Next.CostsExecution && OutOfBudget()) {
                    return false;
                }

                var step = firing.Next;
                firing.Next = null;
                var previousTransfer = _transferringDispatch;
                _transferringDispatch = transfer && step.TransfersReservation ? _dispatches.Peek() : null;

                try {
                    step.Run();
                }
                finally {
                    _transferringDispatch = previousTransfer;
                }

                if (firing.Cancelled) {
                    SettleFlashes(firing);

                    return true;
                }
            }
        }
        catch (Exception error) {
            _error(error);
            firing.Cancelled = true;
            SettleFlashes(firing);

            return true;
        }
        finally {
            _activeFiring = previous;
        }
    }

    // An accepted firing's flashes go out with its effects on the next wired tick; a firing that stops short flashes at once.
    private void SettleFlashes(RuntimeFiring firing)
    {
        if (firing.Flashes.Count == 0) {
            return;
        }

        Item[] items = [.. firing.Flashes];
        firing.Flashes.Clear();

        if (firing.Accepted && firing.HasEffects && !firing.Cancelled) {
            HoldFlashes(items, _cycle + 1);

            return;
        }

        var previous = _activeFiring;
        _activeFiring = null;

        try {
            foreach (var item in items) {
                Flash(item);
            }
        }
        finally {
            _activeFiring = previous;
        }
    }

    private IEnumerable<EvaluationStep> EvaluateFiring(RuntimeFiring firing, bool? negative, object[][]? actors,
        IWiredContextualTrigger? trigger, bool called)
    {
        var context = firing.Context;
        var stack = firing.Stack;
        var selectors = new List<IWiredItem>();

        foreach (var selector in WiredSelectorModule.FiltersLast(stack.OfType<IWiredContextualSelector>().Where(RuntimeSupported),
                     selector => selector.FiltersExisting(context))) {
            var passed = false;
            yield return new(() => passed = InvokeRuntime(selector, context, () => { ComposeSelector(context, selector.Select(context)); return true; }));

            if (!passed) {
                yield break;
            }

            selectors.Add(selector);
        }

        context.Selected = new(
            context.SelectorKinds.HasFlag(WiredSelectionKind.Furni) ? context.SelectorPool.FurniIds : context.Triggering.FurniIds,
            context.SelectorKinds.HasFlag(WiredSelectionKind.Users) ? context.SelectorPool.UserIds : context.Triggering.UserIds);
        var addons = stack.OfType<IWiredContextualAddon>().Where(RuntimeSupported).ToArray();

        foreach (var addon in addons.Where(x => !x.AfterConditions)) {
            var passed = false;
            yield return new(() =>
            {
                passed = InvokeRuntime(addon, context, () => addon.Apply(context));
                ApplySelectorLimits(context);
            });

            if (!passed) {
                yield break;
            }
        }

        ApplySelectorLimits(context);

        // The final trigger gate runs once the selector pool is ready and before any condition. Observed r92k r92k-trigger-gate
        // (AIR WIN63-202610131318-860371703): a walks-on trigger whose selector pool rejected the walked tile flashed nothing in
        // neg-w1..w3 (no trigger, selector or always-true condition), while the accepting control pos-w1/w2 flashed all of them.
        if (trigger != null && !trigger.CanTrigger(context)) {
            yield break;
        }

        // A selector flashes each time its stack runs past the gate.
        foreach (var selector in selectors) {
            Flash(selector);
        }

        var conditions = called ? [] : stack.Where(x => IsKind(x, InteractionType.WiredCondition)).ToArray();
        var scoped = called ? null : context.Policy.Addons.Conditions;
        var grouped = scoped == null ? [] : conditions.Where(c => scoped.ConditionIds.Contains(c.Item.Id)).ToArray();

        // An evaluation add-on that selects none of this stack's conditions applies to all of them.
        if (scoped != null && grouped.Length == 0) {
            grouped = conditions;
        }

        var ordinary = scoped == null ? conditions : conditions.Except(grouped).ToArray();
        var matched = 0;

        foreach (var condition in ordinary) {
            var passed = false;

            foreach (var step in EvaluateCondition(condition, context, actors, result => passed = result)) {
                yield return step;
            }

            if (passed) {
                matched++;
                Flash(condition);
            }
        }

        var conditionsPassed = called || MatchConditions(context.Policy, matched, ordinary.Length);

        if (scoped != null) {
            matched = 0;

            foreach (var condition in grouped) {
                var passed = false;

                foreach (var step in EvaluateCondition(condition, context, actors, result => passed = result)) {
                    yield return step;
                }

                if (passed) {
                    matched++;
                    Flash(condition);
                }
            }

            conditionsPassed &= WiredConditionPolicyEvaluator.Matches(scoped.Mode, matched, grouped.Length, scoped.Count);
        }

        firing.ConditionsPassed = conditionsPassed;

        if (trigger is Modern.Triggers.WiredModernTrigger modern) {
            modern.AfterConditions(context);
        }

        if (context.Event.Kind == WiredEventKind.Variable) {
            if (context.Event.EventItem is { } holderItem) {
                context.Selected.FurniIds.Add(holderItem.Id);
            }

            if (context.Event.TargetUser is { } holderUser) {
                context.Selected.UserIds.Add(holderUser.VirtualId);
            }
        }

        if (negative is { } requested && (requested ? conditionsPassed : !conditionsPassed)) {
            // Observed on hhus (AIR WIN63-202610131318-860371703): a Receive Signal trigger whose condition fails still flashes, with the
            // antenna and the sender in the same batch; its condition and actions stay at rest.
            if (!requested && !called && context.Event.Kind == WiredEventKind.Signal) {
                Flash(firing.Source);
            }

            yield break;
        }

        var actions = stack.Where(x => IsKind(x, InteractionType.WiredEffect) && x.Type != WiredBoxType.AddonRandomEffect
            && (x is IWiredContextualAction action && action.IsNegative) == (negative == null && !conditionsPassed)).ToArray();

        if (negative == null && !conditionsPassed && actions.Length == 0) {
            // Observed on hhus (WIN63-202610131318): a click trigger flips even when its date condition fails; the failed condition
            // and the actions it gates stay at rest.
            Flash(firing.Source);

            yield break;
        }

        foreach (var addon in addons.Where(x => x.AfterConditions)) {
            var passed = false;
            yield return new(() => passed = InvokeRuntime(addon, context, () => addon.Apply(context)));

            if (!passed) {
                yield break;
            }
        }

        if (context.Policy.Addons.ActionPicker is { } stateful) {
            var ids = stateful.Pick(actions.Select(x => x.Item.Id).ToArray()).ToHashSet();
            actions = actions.Where(x => ids.Contains(x.Item.Id)).ToArray();
        }
        else if (context.Policy.ChooseActions is { } picker) {
            actions = picker(actions).Where(actions.Contains).Distinct().ToArray();
        }
        else if (stack.Any(x => x.Type == WiredBoxType.AddonRandomEffect) && actions.Length > 0) {
            actions = [actions[Random.Shared.Next(actions.Length)]];
        }

        var actor = actions.Any(x => x is IWiredFiringPreparation)
            ? context.Targets.ResolveUsers(context, context.Event.Actor == null ? [] : [context.Event.Actor.VirtualId], WiredSources.Selected).FirstOrDefault() : null;
        object[] arguments = actor?.GetClient()?.GetHabbo() is { } habbo ? [habbo] : [];
        var legacy = new WiredExecutionContext(arguments, context.Depth, _actorVisit?.Invoke(arguments), context);
        var ready = new List<IWiredItem>();

        foreach (var action in actions) {
            if (action is IWiredFiringPreparation preparation) {
                var passed = false;
                yield return new(() => passed = Invoke(action, legacy, () => preparation.Prepare(arguments)));

                if (!passed) {
                    continue;
                }
            }

            ready.Add(action);
        }

        yield return new(() =>
        {
            context.VariableChanges = context.Policy.Addons.ExecuteInOrder ? null : new WiredVariableChangeBatch();
            firing.HasEffects = ready.Count > 0;
            var flashFirst = context.ResumeImmediately;
            firing.Accepted = ScheduleActions(firing.Source, stack, ready.ToArray(), legacy,
                action => action is IWiredConfiguredItem configured ? WiredConfigurationLimits.DelayMilliseconds(context.ConfigurationOf(configured).Delay) : GetDelay(action),
                firing.FiredAt, prepared: true, drain: !flashFirst);

            if (firing.Accepted) {
                foreach (var addon in addons) {
                    Flash(addon);
                }

                // Observed r92g r92-signals call-w1/w2: a called stack flashes its actions, not its own trigger.
                if (!called) {
                    Flash(firing.Source);
                }
            }

            if (flashFirst) {
                FlushThenDrain();
            }
        }, CostsExecution: false, TransfersReservation: true);
    }

    private static void ApplySelectorLimits(WiredRuntimeContext context)
    {
        if (context.Policy.Addons.FurniLimit is not >= 0 && context.Policy.Addons.UserLimit is not >= 0) {
            return;
        }

        var pool = new WiredSelectedIds();
        pool.FurniIds.UnionWith(context.SelectorFurniOrder);
        pool.UserIds.UnionWith(context.SelectorUserOrder);
        var filtered = context.Policy.Addons.FilterSelection(pool, context.SelectorFurniOrder, context.SelectorUserOrder);
        context.SelectorFurniOrder.RemoveAll(id => !filtered.FurniIds.Contains(id));
        context.SelectorUserOrder.RemoveAll(id => !filtered.UserIds.Contains(id));
        context.SelectorPool.FurniIds.Clear();
        context.SelectorPool.FurniIds.UnionWith(filtered.FurniIds);
        context.SelectorPool.UserIds.Clear();
        context.SelectorPool.UserIds.UnionWith(filtered.UserIds);
        context.Selected = new(
            context.SelectorKinds.HasFlag(WiredSelectionKind.Furni) ? context.SelectorPool.FurniIds : context.Triggering.FurniIds,
            context.SelectorKinds.HasFlag(WiredSelectionKind.Users) ? context.SelectorPool.UserIds : context.Triggering.UserIds);
    }

    private IEnumerable<EvaluationStep> EvaluateCondition(IWiredItem condition, WiredRuntimeContext context,
        object[][]? actors, Action<bool> result)
    {
        if (actors == null) {
            yield return new(() => result(InvokeRuntime(condition, context, () => ExecuteRuntimeBody(condition, context))));
            yield break;
        }

        foreach (var arguments in actors) {
            var passed = false;

            if (condition is not IWiredContextualItem) {
                yield return new(() => passed = Execute(condition, CreateContext(arguments, context.Depth)));
            }
            else {
                var actor = arguments.OfType<RoomUser>().FirstOrDefault() ?? _actorVisit?.Invoke(arguments) as RoomUser;

                if (actor == null || !context.UserIdentity.TryGetValue(actor.VirtualId, out var original) || !ReferenceEquals(actor, original)
                    || !context.Targets.ResolveUsers(context, [actor.VirtualId], WiredSources.Selected, raw: true).Contains(actor)) {
                    continue;
                }

                var actorContext = context.ForActor(actor);
                yield return new(() => passed = InvokeRuntime(condition, actorContext, () => ExecuteRuntimeBody(condition, actorContext)));
            }

            if (passed) {
                result(true);
                yield break;
            }
        }

        result(false);
    }

    private sealed record EvaluationStep(Action Run, bool CostsExecution = true, bool TransfersReservation = false);
    private sealed class RuntimeFiring(IWiredItem source, IWiredItem[] stack, WiredRuntimeContext context, long firedAt) : IDisposable
    {
        public IWiredItem Source { get; } = source;
        public IWiredItem[] Stack { get; } = stack;
        public WiredRuntimeContext Context { get; } = context;
        public long FiredAt { get; } = firedAt;
        public (IWiredItem Box, long Generation, int X, int Y, double Z)[] Positions { get; } = stack.Select(box => (box, box.Item.MovementGeneration, box.Item.GetX, box.Item.GetY, box.Item.GetZ)).ToArray();
        public IEnumerator<EvaluationStep> Steps { get; set; } = null!;
        public EvaluationStep? Next { get; set; }
        public bool Accepted, Cancelled, ConditionsPassed, HasEffects;
        public List<Item> Flashes { get; } = [];
        public void Dispose() => Steps.Dispose();
    }
}
