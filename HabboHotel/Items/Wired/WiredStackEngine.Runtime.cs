using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using WiredSelectorResult = Plus.HabboHotel.Items.Wired.Runtime.WiredSelectorResult;

namespace Plus.HabboHotel.Items.Wired;

internal sealed partial class WiredStackEngine
{
    private Room? _runtimeRoom;
    private WiredTargetResolver? _targets;
    private IWiredRuntimeOperations? _operations;
    private WiredRuntimeContext? _runtimeContext;
    private WiredRuntimeEvent? _legacyRuntimeEvent;
    public Action<WiredRuntimeEvent, long>? ObserveEvent { get; set; }
    public Action<RoomUser>? LeaverSettled { get; set; }
    private readonly Dictionary<RoomUser, LeaverWindow> _leaverWindows = new(ReferenceEqualityComparer.Instance);
    public Func<WiredRuntimeContext, IWiredContextualTrigger, bool?>? CaptureSpeech { get; set; }
    public Action<WiredRuntimeEvent>? SpeechHidden { get; set; }
    private bool _legacySpeechAdmission;
    private IWiredItem? _legacySpeechOrigin;
    private readonly Queue<PendingDispatch> _dispatches = new();
    private Func<bool>? _externalFastWork;
    private Action<long>? _pollExternal;
    private Action? _flushExternal;
    private int? _queuedDepth;
    public Action<IWiredConfiguredItem>? ConfigurationPublished { get; set; }
    /// <summary>Opened when the outermost pass starts and disposed when it ends.</summary>
    public Func<IDisposable?>? PassScope { get; set; }
    private readonly Dictionary<uint, (int X, int Y, double Z)> _runtimePositions = [];
    private int _fastWork;
    private Action<bool>? _fastWorkObserver;
    private int _timerCursor;

    public void BindRuntime(Room room, WiredTargetResolver targets, IWiredRuntimeOperations operations,
        Func<bool>? needsFast = null, Action<long>? poll = null, Action? flush = null)
    {
        lock (_sync) {
            _runtimeRoom = room;
            _targets = targets;
            _operations = operations;
            _externalFastWork = needsFast;
            _pollExternal = poll;
            _flushExternal = flush;
        }
    }

    public long NowMilliseconds => _now();

    public bool Mutate(Func<bool> mutation) => Pass(() =>
    {
        SealPublication();

        try {
            return mutation();
        }
        finally {
            UpdateFastWork();
        }
    });

    /// <summary>
    /// While a wired disabler is on, no wired runs in the room: no trigger fires and the repeaters and timers stand still. Placing and saving boxes still works.
    /// </summary>
    private bool Disabled
    {
        get
        {
            var disabled = _runtimeRoom is { } room && ConfigurationItemStates.WiredDisabled(room);
            TrackDisabled(disabled);

            return disabled;
        }
    }

    private long? _disabledSince;

    /// <summary>
    /// A delay that is running when the disabler goes on is paused and goes on with the time it had left once the disabler is off. Time stands still in whole room ticks: every waiting step moves later by the ticks that passed.
    /// </summary>
    private void TrackDisabled(bool disabled)
    {
        if (disabled) {
            _disabledSince ??= _now();

            return;
        }

        if (_disabledSince is not { } since) {
            return;
        }

        _disabledSince = null;
        const long Tick = WiredFlashStates.StateUpdateMilliseconds;
        var paused = (_now() / Tick - since / Tick) * Tick;

        if (paused <= 0) {
            return;
        }

        var waiting = _schedule.UnorderedItems.ToArray();
        _schedule.Clear();

        foreach (var (element, priority) in waiting) {
            _schedule.Enqueue(element, (priority.Due + paused, priority.Height, priority.Id, priority.Firing, priority.Order));
        }

        foreach (var chain in _pending.Where(chain => chain.FirstResumeAt != null)) {
            chain.FirstResumeAt += paused;
        }
    }

    public bool Enqueue(WiredRuntimeEvent @event, int? depth = null) => Pass(() => !Disabled && EnqueueCore(@event, depth));

    // The events that report a change: a collision, a variable, a score, a furni state, a clock value or a link message. Their stacks run in the pass of
    // the change (see DrainSamePassEvents), not one tick later like those of a click or a walk.
    private static bool RunsInPassOfChange(WiredEventKind kind) => kind is WiredEventKind.Collision or WiredEventKind.Variable
        or WiredEventKind.Score or WiredEventKind.StateChanged or WiredEventKind.Counter
        or WiredEventKind.LinkMessage or WiredEventKind.LinkDataframe;

    /// <summary>
    /// Runs the stacks of a furni state change at once. A change that a running stack made waits in the queue of its pass instead, and
    /// leaves the open publication to that queued dispatch, which seals it only when a stack runs (see BeforePublicationDispatch).
    /// Returns whether the stacks ran at once and whether the event was accepted.
    /// </summary>
    public (bool RanAtOnce, bool Accepted) DispatchStateChange(WiredRuntimeEvent @event) => Pass(() =>
    {
        try {
            if (_runtimeContext != null) {
                return (false, !Disabled && EnqueueCore(@event));
            }

            SealPublication();

            return (true, DispatchCore(@event, resumeImmediately: true).Accepted);
        }
        finally {
            UpdateFastWork();
        }
    });

    private bool EnqueueCore(WiredRuntimeEvent @event, int? depth = null, HeadingCollisionProof? collision = null, bool sealOnRejection = false)
    {
        var eventDepth = depth ?? (_runtimeContext?.Depth ?? -1) + 1;

        if (sealOnRejection && eventDepth > _limits.MaxDepth) {
            SealPublication();
        }

        if (TooDeep(eventDepth) || _runtimeRoom == null) {
            return false;
        }

        RefreshStacks();
        var samePass = RunsInPassOfChange(@event.Kind);
        // A Walks On or Walks Off that the room tick raised runs in that tick and its output follows the tick's UserUpdate in the same ms; a delay counts from the tick.
        var walkInTick = _roomTickOpen && @event.Kind is WiredEventKind.WalkOn or WiredEventKind.WalkOff;
        // The stacks of a Bot Reaches Furni also run at once, because a Bot Teleport raises it in the tick that republishes the bot.
        var dispatch = new PendingDispatch(@event, eventDepth)
        {
            CollisionProof = collision,
            SamePass = samePass,
            ResumeImmediately = samePass || walkInTick || @event.Kind == WiredEventKind.BotReachedFurni,
            FiredAt = walkInTick ? LastRoomTick : null
        };
        SnapshotDispatch(dispatch);

        if (sealOnRejection && PendingCount + dispatch.Slots > _limits.MaxPendingStacks) {
            SealPublication();
        }

        if (QueueFull(dispatch.Slots)) {
            return false;
        }

        QueueDispatch(dispatch);
        UpdateFastWork();

        return true;
    }

    public void ActorLeaving(RoomUser actor) => Pass(() =>
    {
        // A runtime chain outlives its actor; its steps check that their own user is still in the room.
        _pending.RemoveWhere(chain => chain.Context.Runtime == null && ReferenceEquals(chain.Context.ActorVisit, actor));
        PruneSchedule();
        var kept = _dispatches.Where(pending => pending.Event.Kind == WiredEventKind.Leave
            || !ReferenceEquals(pending.Event.Actor, actor)).ToArray();

        foreach (var pending in _dispatches.Except(kept)) {
            pending.Current?.Dispose();
        }

        foreach (var pending in _dispatches) {
            pending.IsQueued = false;
        }

        _dispatches.Clear();
        _queuedSlots = 0;

        foreach (var pending in kept) {
            QueueDispatch(pending);
        }

        UpdateFastWork();

        return true;
    });

    public bool NeedsFastCycle => Volatile.Read(ref _fastWork) != 0;

    public void ObserveFastWork(Action<bool>? observer)
    {
        lock (_sync) {
            _fastWorkObserver = observer;
            observer?.Invoke(NeedsFastCycle);
        }
    }

    // Compatibility seam for synchronous speech decisions and explicit engine evaluations.
    // Production events otherwise enter through Enqueue and evaluate on the next room boundary.
    public bool DispatchSynchronously(WiredRuntimeEvent @event) => Pass(() => DispatchCore(@event).Accepted);

    /// <summary>Runs the stacks of an event at once, for the room load that happens before any room tick.</summary>
    public bool DispatchOnLoad(WiredRuntimeEvent @event) => Pass(() => DispatchCore(@event, resumeImmediately: true).Accepted);

    /// <summary>Runs the stacks of an event and their effects at once, outside the room's passes.</summary>
    public bool DispatchAtOnce(WiredRuntimeEvent @event) => Pass(() => DispatchCore(@event, resumeImmediately: true).Accepted);

    public WiredClickResult DispatchClickUser(RoomUser actor, RoomUser target) => Pass(() =>
    {
        if (_targets == null || !_targets.AllUsers().Contains(actor) || !_targets.AllUsers().Contains(target)) {
            return default;
        }

        return DispatchCore(new(WiredEventKind.ClickUser) { Actor = actor, TargetUser = target }).Click;
    });

    private (bool Accepted, WiredClickResult Click) DispatchCore(WiredRuntimeEvent @event, bool resumeImmediately = false)
    {
        var previousHold = _speechPass;
        _speechPass = previousHold || @event.Kind == WiredEventKind.Speech;

        try {
            return DispatchCoreUnheld(@event, resumeImmediately);
        }
        finally {
            _speechPass = previousHold;
        }
    }

    private (bool Accepted, WiredClickResult Click) DispatchCoreUnheld(WiredRuntimeEvent @event, bool resumeImmediately)
    {
        if (_runtimeRoom == null || Disabled) {
            return default;
        }

        var depth = _queuedDepth ?? (_runtimeContext?.Depth ?? _context?.Depth ?? -1) + 1;
        _queuedDepth = null;

        if (TooDeep(depth)) {
            return default;
        }

        RefreshStacks();
        var dispatch = new PendingDispatch(@event, depth) { IncludeLegacy = false, ResumeImmediately = @event.Kind == WiredEventKind.Speech || resumeImmediately };
        var complete = AdvanceDispatch(dispatch);

        if (!complete) {
            // A synchronous speech decision cannot consume chat on an unfinished condition gate.
            if (@event.Kind is not (WiredEventKind.Speech or WiredEventKind.ClickUser) && !QueueFull(dispatch.Slots)) {
                QueueDispatch(dispatch);
                dispatch.Accepted = true;
            }
            else {
                dispatch.Current?.Dispose();
                dispatch.LeaverWindow?.Dispatched = true;
                SettleLeavers();
            }
        }

        DrainReadySignals();
        DrainSamePassEvents();
        UpdateFastWork();

        if (dispatch.ConsumedChat) {
            SpeechHidden?.Invoke(@event);
        }

        return (@event.Kind == WiredEventKind.Speech ? dispatch.ConsumedChat : dispatch.Accepted, dispatch.Click);
    }

    public bool DispatchLegacy(WiredBoxType type, WiredRuntimeEvent? typed, object[] arguments) => Pass(() =>
    {
        var previous = _legacyRuntimeEvent;
        var previousSpeechAdmission = _legacySpeechAdmission;
        _legacySpeechAdmission = typed?.Kind == WiredEventKind.Speech;
        var queuedDepth = _queuedDepth;
        _legacyRuntimeEvent = typed;

        try {
            var accepted = Dispatch(type, arguments);

            if (typed != null) {
                _queuedDepth = queuedDepth;
                // An Enter Room stack runs inside the room entry, like a speech stack with its chat.
                accepted |= typed.Kind switch
                {
                    WiredEventKind.Speech => DispatchSynchronously(typed),
                    WiredEventKind.Enter => DispatchCore(typed, resumeImmediately: true).Accepted,
                    _ => Enqueue(typed, _queuedDepth)
                };
            }

            return accepted;
        }
        finally {
            _legacyRuntimeEvent = previous;
            _legacySpeechAdmission = previousSpeechAdmission;
        }
    });

    public bool CallStacks(WiredRuntimeContext parent, IEnumerable<Item> targets, bool negative = false) => Pass(() =>
    {
        if (!ReferenceEquals(parent.Room, _runtimeRoom) || Disabled || TooDeep(parent.Depth + 1)) {
            return false;
        }

        var accepted = false;
        var visited = new HashSet<(int, int)>();

        foreach (var item in targets.OrderBy(x => x.GetZ).ThenBy(x => x.Id).ToArray()) {
            if (OutOfBudget()) {
                break;
            }

            if (!parent.Targets.IsAttached(item) || !parent.FurniIdentity.TryGetValue(item.Id, out var captured)
                || !ReferenceEquals(captured, item)) {
                continue;
            }

            RefreshStacks();

            if (!_stacks.TryGetValue((item.GetX, item.GetY), out var tile)) {
                continue;
            }

            var source = tile.FirstOrDefault(IsAttached);

            if (source == null || !visited.Add((item.GetX, item.GetY))) {
                continue;
            }

            var dispatch = new PendingDispatch(parent.Event, parent.Depth + 1,
                call: new(source, parent, parent.Selected.Copy()))
            { ResumeImmediately = true, Publication = parent.Publication };
            SnapshotDispatch(dispatch);

            if (QueueFull(dispatch.Slots)) {
                continue;
            }

            // A call invokes its whole action stack, rather than publishing another external event.
            // Native speech controls establish same-pass zero effects; other origins use the same box rule.
            if (!AdvanceDispatch(dispatch)) {
                QueueDispatch(dispatch);
                accepted = true;
            }
            else {
                accepted |= dispatch.Accepted;
            }
        }

        UpdateFastWork();

        return accepted;
    });

    internal static bool IsSignalAntenna(Item item) => item.IsFloorItem
        && string.Equals(item.Definition.InteractionName, "antenna", StringComparison.OrdinalIgnoreCase);

    public bool SendSignal(WiredRuntimeContext parent, IEnumerable<Item> receivers,
        WiredSelection selection, bool negative = false) => Pass(() =>
    {
        if (!ReferenceEquals(parent.Room, _runtimeRoom) || Disabled) {
            return false;
        }

        var antennas = receivers.DistinctBy(x => x.Id).Where(receiver => IsSignalAntenna(receiver) && _targets!.IsAttached(receiver)).ToArray();

        if (antennas.Length == 0) {
            return false;
        }

        var accepted = false;

        // A loop that waits before it signals again keeps running past 100 runs, so a delayed Send Signal starts one new count for all its antennas and batches.
        if (_executingAction is { DelayMilliseconds: > 0 } step && !ReferenceEquals(parent.SignalChainStep, step)) {
            parent.SignalChainRuns = [];
            parent.SignalChainStep = step;
        }

        foreach (var antenna in antennas) {
            // Official delivery fires once per addressed antenna matching a receiving stack's picks.
            // A straight chain of signals runs every stack, and each receiving stack in a loop runs 100 times. A hop therefore keeps the sender's depth; the run cap (SignalRunCapReached) bounds a loop instead.
            parent.SignalChainRuns ??= [];
            var child = parent.Fork(new(WiredEventKind.Signal)
            {
                Code = unchecked((int)antenna.Id),
                Actor = parent.Event.Actor,
                EventItem = parent.Event.EventItem,
                SignalLeaver = LeaverOf(parent.Event) is { } leaver && _targets!.Leavers.Contains(leaver) ? leaver : null
            }, parent.Depth);
            child.Signal = new(selection, parent.Values);
            // Routing antennas and forwarded targets are independent from the original event's targets.
            child.Triggering = parent.Triggering.Copy();
            child.Selected = child.Triggering.Copy();
            var dispatch = new PendingDispatch(child.Event, child.Depth,
                new([(antenna, antenna.MovementGeneration)], child, parent))
            { ResumeImmediately = true, Publication = parent.Publication };
            // Receivers run in the sender's tick, after the sender stack's remaining actions, and two receivers on one antenna run in a varying order:
            // neither placement, id, height nor tile fixes the order, so receivers keep the per-event random order.
            SnapshotDispatch(dispatch);

            if (QueueFull(dispatch.Slots)) {
                break;
            }

            QueueDispatch(dispatch);
            Flash(antenna);
            accepted = true;
        }

        UpdateFastWork();

        return accepted;
    });

    /// <summary>
    /// Runs <paramref name="callback"/> as part of the executing action's chain. With <paramref name="alignToStateUpdate"/> it waits for
    /// the next 500 ms state update boundary, the cycle that also carries the box flashes.
    /// </summary>
    public bool ScheduleAux(WiredRuntimeContext context, int delayMilliseconds, Action callback, Action? onCancelled = null,
        bool alignToStateUpdate = false) => Pass(() =>
    {
        var active = _executingAction;

        if (active == null || !ReferenceEquals(active.Chain.Context.Runtime, context)
            || !CanSchedule([active.Box])) {
            return false;
        }

        var chain = new ActionChain(active.Chain.Source, active.Chain.Stack, active.Chain.Context, 1);
        _pending.Add(chain);
        var due = DueAt(_now() + Math.Max(0, delayMilliseconds), alignToStateUpdate);
        _schedule.Enqueue(new(chain, active.Box, callback, onCancelled), (due, active.Box.Item.GetZ, active.Box.Item.Id, ++_sequence, 0));

        // Like a delayed effect, a timed step wakes the room at its due time instead of waiting for the next 50 ms step.
        if (due > _now()) {
            ActionDue?.Invoke(due);
        }

        UpdateFastWork();

        return true;
    });

    /// <summary>
    /// Schedules a step of the running action for the room tick whose 500 ms boundary is <paramref name="tickBoundary"/>. It runs at the
    /// start of that tick's room pass (<see cref="OnRoomTick"/>), so what it changes goes out with that tick's avatar updates.
    /// </summary>
    public bool ScheduleOnRoomTick(WiredRuntimeContext context, long tickBoundary, Action callback, Action? onCancelled = null,
        bool afterAvatarUpdates = false) => Pass(() =>
    {
        var active = _executingAction;

        if (active == null || !ReferenceEquals(active.Chain.Context.Runtime, context)
            || !CanSchedule([active.Box])) {
            return false;
        }

        var chain = new ActionChain(active.Chain.Source, active.Chain.Stack, active.Chain.Context, 1);
        _pending.Add(chain);
        _schedule.Enqueue(new(chain, active.Box, callback, onCancelled) { OnRoomTick = true, AfterAvatarUpdates = afterAvatarUpdates },
            (tickBoundary, active.Box.Item.GetZ, active.Box.Item.Id, ++_sequence, 0));
        UpdateFastWork();

        return true;
    });

    /// <summary>The boundary of the last room tick that started, or null before the first one.</summary>
    public long? LastRoomTick { get; private set; }
    /// <summary>The number of room ticks that started. Work between two room ticks shares one count.</summary>
    public long RoomTickIndex { get; private set; }

    /// <summary>
    /// The boundary of the first room tick after <paramref name="firing"/>: the tick after the last one that started, or a tick that is
    /// due but has not started yet. Without room ticks (no room pass yet), the next 500 ms boundary.
    /// </summary>
    public long FirstRoomTickAfter(long firing)
    {
        var interval = WiredFlashStates.StateUpdateMilliseconds;

        return LastRoomTick is { } last
            ? Math.Max(last + interval, firing / interval * interval)
            : (firing / interval + 1) * interval;
    }

    /// <summary>
    /// Runs the steps that were scheduled with <c>afterAvatarUpdates</c> for this tick, once the room has sent the avatars' status
    /// updates. Only has work after <see cref="OnRoomTick"/> was told to split the end.
    /// </summary>
    public void OnRoomTickEnd() => Pass(() =>
    {
        _roomTickOpen = false;

        if (!_splitEnd || Disabled) {
            return false;
        }

        _splitEnd = false;
        _roomTickDrain = true;
        _endPhase = true;

        try {
            DrainDueActions();
        }
        finally {
            _roomTickDrain = false;
            _endPhase = false;
        }

        UpdateFastWork();

        return true;
    });

    /// <summary>Starts a room tick: records its boundary and runs the steps scheduled for it, before the room moves and serializes avatars.</summary>
    public void OnRoomTick(bool splitEnd = false) => Pass(() =>
    {
        if (Disabled) {
            return false;
        }

        var now = _now();
        LastRoomTick = now - now % WiredFlashStates.StateUpdateMilliseconds;
        RoomTickIndex++;
        _roomTickOpen = true;
        _roomTickDrain = true;
        _splitEnd = splitEnd;

        try {
            DrainDueActions();
        }
        finally {
            _roomTickDrain = false;
        }

        UpdateFastWork();

        return true;
    });

    private bool _roomTickDrain;
    // From the start of a room tick until its avatar updates are sent: the walks its actors and rollers raise run in it. A walk that a
    // Wired effect raises later in the pass waits one pass, as it does from a fast pass.
    private bool _roomTickOpen;
    private bool _splitEnd;
    private bool _endPhase;

    private static long DueAt(long due, bool alignToStateUpdate) => alignToStateUpdate
        ? (due + WiredFlashStates.StateUpdateMilliseconds - 1) / WiredFlashStates.StateUpdateMilliseconds * WiredFlashStates.StateUpdateMilliseconds
        : due;

    public void ResetTimers(IEnumerable<Item> targets) => Pass(() =>
    {
        // One instant for the whole reset, and each stack once however many of its boxes are targeted.
        var now = _now();
        var tiles = new HashSet<(int X, int Y)>();

        foreach (var target in targets) {
            if (!_items.TryGetValue(target.Id, out var source) || !ReferenceEquals(source.Item, target)
                || !IsAttached(source) || !tiles.Add((source.Item.GetX, source.Item.GetY))) {
                continue;
            }

            // Room timer reset restarts at-time elapsed and armed repeaters from the first room tick after it.
            foreach (var box in GetStack(source)) {
                if (box is IWiredTimedTrigger timer) {
                    timer.ResetElapsed(now);
                }
            }
        }

        return true;
    });

    public bool PublishConfigured(IWiredConfiguredItem original, WiredConfiguration validated, Action persist) => Pass(() =>
    {
        if (!IsAttached(original) || !RuntimeSupported(original)
            || WiredNativeEditorProjection.Supports(original.Descriptor.CanonicalName)
                && !WiredNativeEditorProjection.IsBound(original.Item.Id, original.Descriptor, validated)) {
            return false;
        }

        persist();
        CancelPending(original);
        original.ApplyConfiguration(validated);
        ConfigurationPublished?.Invoke(original);
        ResetRuntimeTile(original.Item.GetX, original.Item.GetY);
        UpdateFastWork();

        return true;
    });

    public void OnFastCycle() => Pass(() =>
    {
        if (_runtimeRoom == null || !NeedsFastCycle || Disabled) {
            return false;
        }

        _cycle++;
        ReleaseHeldFlashes();
        RefreshStacks();
        DrainDueActions();
        RunRuntimeTimersAndSignals();
        DrainDueActions();
        DrainSamePassEvents();
        _roomTickOpen = false;
        UpdateFastWork();

        return true;
    });

    private WiredRuntimeContext CreateContext(WiredRuntimeEvent @event, int depth) =>
        new(_runtimeRoom!, @event, _targets!, _operations!) { Depth = depth, NowMilliseconds = _now() };

    private static void SeedEvent(WiredRuntimeContext context)
    {
        if (context.Event.EventItem != null && context.Event.Kind != WiredEventKind.Variable) {
            context.Triggering.FurniIds.Add(context.Event.EventItem.Id);
        }

        // Variable Changed on a user variable shows a triggering-user message from that user; on a global it fires with no triggering user (the message is not shown), whoever ran the change.
        var user = context.Event.Kind switch
        {
            // Bot Reached Avatar + Bot Talk To Avatar (avatar: triggering user) says nothing, so the triggering user is the bot; the avatar it reached is the Reached User source.
            WiredEventKind.BotReachedUser => context.Event.Actor,
            WiredEventKind.Variable => context.Event.TargetUser,
            // Bot Reached Furni + Show Message (triggering user) is said by the bot.
            WiredEventKind.BotReachedFurni => context.Event.Actor,
            WiredEventKind.StateChanged => null,
            _ => context.Event.Actor
        };

        if (user != null) {
            context.Triggering.UserIds.Add(user.VirtualId);
        }

        context.Selected = context.Triggering.Copy();
    }

    private bool RunRuntimeStack(IWiredItem source, WiredRuntimeContext context, bool? negative, object[][]? conditionActors = null, bool defer = true,
        long? firedAt = null)
    {
        if (TooDeep(context.Depth) || !IsAttached(source)) {
            return false;
        }

        var firing = BeginFiring(source, context, negative, conditionActors, firedAt: firedAt);

        if (ResumeFiring(firing)) {
            firing.Dispose();
            DrainReadySignals();
            UpdateFastWork();

            return firing.Accepted;
        }

        // Chat consumption requires a completed synchronous decision. Do not defer its gate.
        if (!defer || context.Event.Kind == WiredEventKind.Speech || QueueFull(1)) {
            firing.Dispose();

            return false;
        }

        QueueDispatch(new(context.Event, context.Depth) { Current = firing, Triggers = [], Initialized = true });
        UpdateFastWork();

        return true;
    }

    private static bool MatchConditions(WiredExecutionPolicy policy, int matched, int total) => total == 0 || policy.ConditionMode switch
    {
        WiredConditionMode.Any => matched > 0,
        WiredConditionMode.None => true,
        WiredConditionMode.NoneMatch => matched == 0,
        WiredConditionMode.NotAll => matched < total,
        WiredConditionMode.LessThan => matched < policy.ConditionThreshold,
        WiredConditionMode.Exactly => matched == policy.ConditionThreshold,
        WiredConditionMode.MoreThan => matched > policy.ConditionThreshold,
        _ => matched == total
    };

    private void ComposeSelector(WiredRuntimeContext context, WiredSelectorResult result)
    {
        void Apply<T>(HashSet<T> pool, List<T> order, HashSet<T> selected, IEnumerable<T> universe, WiredSelectionKind kind)
        {
            if (!result.Kind.HasFlag(kind)) {
                return;
            }

            var kept = (result.Invert ? universe.Except(selected) : selected).ToArray();

            if (result.FiltersExisting) {
                // A filter with no selector of its kind before it selects nothing; it does not fall back to the triggering user.
                var basis = context.SelectorKinds.HasFlag(kind) ? order.ToArray() : [];
                var matches = kept.ToHashSet();
                order.Clear();
                order.AddRange(basis.Where(matches.Contains));
                pool.Clear();
                pool.UnionWith(order);
            }
            else {
                foreach (var id in kept) {
                    if (pool.Add(id)) {
                        order.Add(id);
                    }
                }
            }

            context.SelectorKinds |= kind;
        }
        // Selectors include temporary copies; manual picks still refuse them. The inversion universe also includes wall furniture.
        Apply(context.SelectorPool.FurniIds, context.SelectorFurniOrder, result.Selection.FurniIds,
            context.FurniIdentity.Keys, WiredSelectionKind.Furni);
        Apply(context.SelectorPool.UserIds, context.SelectorUserOrder, result.Selection.UserIds,
            context.UserIdentity.Keys, WiredSelectionKind.Users);
    }

    private bool InvokeRuntime(IWiredItem box, WiredRuntimeContext context, Func<bool> invoke)
    {
        if (PublicationEvaluationIsSilent?.Invoke(box) != true) {
            SealPublication();
        }

        if (OutOfBudget() || !IsAttached(box)) {
            return false;
        }

        _remaining--;
        _passPeakDepth = Math.Max(_passPeakDepth, context.Depth);
        var previous = _runtimeContext;
        _runtimeContext = context;
        context.NowMilliseconds = _now();

        try {
            return invoke();
        }
        catch (Exception error) {
            SealPublication();
            _error(error);

            return false;
        }
        finally {
            _runtimeContext = previous;
        }
    }

    private void FlushExternal()
    {
        if (_flushExternal == null) {
            return;
        }

        if (PublicationFlushIsSilent?.Invoke() != true) {
            SealPublication();
        }

        _flushExternal();
    }

    private bool ExecuteRuntimeBody(IWiredItem box, WiredRuntimeContext context)
    {
        if (box is IWiredContextualItem contextual) {
            try {
                return RuntimeSupported(contextual) && contextual.Execute(context);
            }
            finally {
                FlushExternal();
            }
        }

        var actor = context.Event.Actor;

        if (actor == null) {
            return box.Execute([]);
        }

        if (!context.Targets.ResolveUsers(context, [actor.VirtualId], WiredSources.Selected).Contains(actor)) {
            return false;
        }

        return box.Execute(actor.GetClient()?.GetHabbo());
    }

    private void RunRuntimeTimersAndSignals()
    {
        var now = _now();

        if (_pollExternal != null) {
            if (PublicationPollIsSilent?.Invoke() != true) {
                SealPublication();
            }

            _pollExternal(now);
        }

        // Every pass polls: the room paces passes, and each timer keeps its own deadlines.
        WiredRuntimeContext? snapshot = null;
        var timers = _items.Values.OfType<IWiredTimedTrigger>().Where(RuntimeSupported)
            .OrderBy(IsRepeatShort).ThenBy(x => x.Item.GetZ).ThenBy(x => x.Item.Id).ToArray();
        var roomTickTimerFired = false;

        var dormant = timers.Any(timer => timer is WiredModernTimedTrigger)
            && !_targets!.AllUsers().Any(user => !user.IsBot || user.IsWalking);

        for (var polled = 0; polled < timers.Length && !OutOfBudget(); polled++) {
            _timerCursor %= timers.Length;
            var timer = timers[_timerCursor];
            _timerCursor = (_timerCursor + 1) % timers.Length;

            if (!IsAttached(timer) || dormant && timer is WiredModernTimedTrigger) {
                continue;
            }

            try {
                // Native modern timer polling only reads deadlines; custom callbacks may publish.
                if (timer is not WiredModernTimedTrigger) {
                    SealPublication();
                }

                if (timer.Poll(now) is not { } @event) {
                    continue;
                }

                // An isolated timer has no work that can observe an intermediate furniture pose.
                // Its flash is buffered like the other box flashes until the room's state update.
                if (timer is not WiredModernTimedTrigger
                    || !_stacks.TryGetValue((timer.Item.GetX, timer.Item.GetY), out var timerStack)
                    || timerStack.Any(box => !ReferenceEquals(box, timer))) {
                    SealPublication();
                }

                // A Repeat Short that fires after a room tick timer sends its own ObjectsDataUpdate.
                if (IsRepeatShort(timer) && roomTickTimerFired) {
                    roomTickTimerFired = false;
                    SealPublication();
                    _runtimeRoom?.FlushWiredFlashData();
                }

                roomTickTimerFired |= !IsRepeatShort(timer);
                snapshot ??= CreateContext(@event, 0);
                var context = snapshot.Fork(@event, 0);
                context.Trigger = timer;
                // A timer's stack runs on the Wired tick it is due and its output goes out in that tick, after the room tick's UserUpdate; a delay counts from that tick.
                context.ResumeImmediately = true;
                SeedEvent(context);

                if (RunRuntimeStack(timer, context, null, firedAt: now - now % WiredFlashStates.WiredTickMilliseconds)) {
                    Flash(timer.Item);
                }
            }
            catch (Exception error) {
                _error(error);
            }
        }

        // Accepted envelopes retain their next trigger and current stack evaluation across passes.
        while (_dispatches.TryPeek(out var pending) && !OutOfBudget()) {
            if (!AdvanceDispatch(pending)) {
                break;
            }

            if (_dispatches.TryPeek(out var head) && ReferenceEquals(head, pending)) {
                RemoveDispatchHead();
            }

            DrainReadySignals();
        }

        DrainReadySignals();
    }

    private static bool IsRepeatShort(IWiredTimedTrigger timer) => timer is WiredModernTimedTrigger { Descriptor.CanonicalName: "wf_trg_period_short" };

    private bool _drainingSignals;
    private bool _speechPass;

    /// <summary>
    /// True when the chat that this context shows must wait for the echo of the say: the say is still running its stacks and the context
    /// belongs to a stack that a Send Signal reached. A delayed action runs after the say and is not held.
    /// </summary>
    internal bool OutputFollowsEcho(WiredRuntimeContext context) => _speechPass && context.Event.Kind == WiredEventKind.Signal;

    /// <summary>True when a say left stacks of events it raised, such as Variable Changed or Score Achieved, for <see cref="DrainVariableStacks"/>.</summary>
    public bool HasHeldVariableStacks
    {
        get
        {
            // Chat runs on a network thread while the room's fast cycle dequeues on its own thread.
            lock (_sync) {
                return _dispatches.TryPeek(out var pending) && pending.SamePass && pending.Event.Kind != WiredEventKind.Collision;
            }
        }
    }

    /// <summary>Runs the stacks of the events that a say raised. The caller sends the echo of the say first.</summary>
    public void DrainVariableStacks() => Pass(() =>
    {
        DrainSamePassEvents();
        UpdateFastWork();

        return true;
    });

    private bool _drainingSamePass;

    // Variable Changed stacks run in the pass of the change, so their messages share its millisecond. After a say they follow the echo of the say, which DrainVariableStacks runs.
    // A Score Achieved stack runs the same way, in the pass of the score change. The same holds when a click fired the Give Score or Toggle State, and for a clock tick.
    // The watching stack flashes in the millisecond of the change, not one tick later.
    //
    // User Stuff Collision raised by an effect runs in the same pulse as that effect, its flashes and chat in the same batch. A Collision at the head of the queue therefore runs
    // before the pass ends, with the effects it schedules; the pass budget and the event depth still bound a chain of them.
    private void DrainSamePassEvents()
    {
        if (_drainingSamePass || _draining || _dispatchAdvanceDepth != 0 || _activeFiring != null) {
            return;
        }

        _drainingSamePass = true;

        try {
            while (_dispatches.TryPeek(out var pending) && pending.SamePass && (pending.Event.Kind == WiredEventKind.Collision || !_speechPass)
                && !OutOfBudget()) {
                if (!AdvanceDispatch(pending)) {
                    break;
                }

                if (_dispatches.TryPeek(out var head) && ReferenceEquals(head, pending)) {
                    RemoveDispatchHead();
                }

                DrainDueActions();
                // A batched change reaches the module when its stack ends; its Variable Changed stacks follow in this pass.
                FlushExternal();
            }
        }
        finally {
            _drainingSamePass = false;
        }
    }

    private void DrainReadySignals()
    {
        // Continue only ready signal envelopes. An earlier external event retains its FIFO position, but the state change of a toggled furni that no stack listens to is passed in order.
        // A signal loop whose stacks toggle furni runs and flashes in the tick of its first signal, in one ObjectsDataUpdate.
        // An active evaluation may still own the head reservation, so admission must never re-enter it.
        if (_drainingSignals || _draining || _dispatchAdvanceDepth != 0 || _activeFiring != null) {
            return;
        }

        _drainingSignals = true;

        try {
            while (_dispatches.TryPeek(out var pending) && (pending.Signal != null || IdleStateChange(pending)) && !OutOfBudget()) {
                if (!AdvanceDispatch(pending)) {
                    break;
                }

                if (_dispatches.TryPeek(out var head) && ReferenceEquals(head, pending)) {
                    RemoveDispatchHead();
                }
            }
        }
        finally {
            _drainingSignals = false;
            UpdateFastWork();
        }
    }

    private static bool RunsNoStack(PendingDispatch pending) => pending.Call == null && pending.Signal == null && pending.Current == null
        && pending.Triggers is { Length: 0 } && pending.LegacyTriggers.Length == 0;

    private static bool IdleStateChange(PendingDispatch pending) => pending.Event.Kind == WiredEventKind.StateChanged && RunsNoStack(pending);

    private static bool RuntimeSupported(IWiredConfiguredItem box) => box.Descriptor.Support == WiredBoxSupport.Implemented;

    private void ResetRuntimeTile(int x, int y)
    {
        foreach (var addon in _items.Values.OfType<IWiredContextualAddon>().Where(a => a.Item.GetX == x && a.Item.GetY == y)) {
            addon.Reset();
        }

        foreach (var timer in _items.Values.OfType<IWiredTimedTrigger>().Where(a => a.Item.GetX == x && a.Item.GetY == y)) {
            timer.Reset(_now());
        }
    }

    private void UpdateRuntimeItems()
    {
        foreach (var box in _items.Values) {
            var position = (box.Item.GetX, box.Item.GetY, box.Item.GetZ);

            if (_runtimePositions.TryGetValue(box.Item.Id, out var previous) && previous != position) {
                ResetRuntimeTile(previous.X, previous.Y);
                ResetRuntimeTile(position.GetX, position.GetY);
            }

            _runtimePositions[box.Item.Id] = position;
        }

        foreach (var id in _runtimePositions.Keys.Except(_items.Keys).ToArray()) {
            _runtimePositions.Remove(id);
        }

        UpdateFastWork();
    }

    private void UpdateFastWork()
    {
        var required = _runtimeRoom != null && HasRuntimeWork();
        var next = required ? 1 : 0;

        if (Interlocked.Exchange(ref _fastWork, next) != next) {
            _fastWorkObserver?.Invoke(required);
        }
    }

    private bool HasRuntimeWork()
    {
        if (_dispatches.Count > 0 || _externalFastWork?.Invoke() == true) {
            return true;
        }

        foreach (var box in _items.Values) {
            if (box is IWiredTimedTrigger timer && RuntimeSupported(timer)) {
                return true;
            }
        }

        foreach (var chain in _pending) {
            if (chain.Context.Runtime != null) {
                return true;
            }
        }

        return false;
    }

    private sealed class PendingDispatch(WiredRuntimeEvent @event, int depth, PendingSignal? signal = null, PendingCall? call = null)
    {
        public WiredRuntimeEvent Event { get; } = @event;
        public int Depth { get; } = depth;
        public PendingSignal? Signal { get; } = signal;
        public PendingCall? Call { get; } = call;
        public IWiredContextualTrigger[]? Triggers;
        public WiredRuntimeContext? Root;
        public int Next;
        public RuntimeFiring? Current;
        public IWiredItem[] LegacyTriggers = [];
        public int LegacyNext;
        public IWiredItem[] CapturedBoxes = [];
        public (IWiredItem Box, int X, int Y, double Z, long Generation)[] CapturedPositions = [];
        public Dictionary<IWiredContextualTrigger, (int X, int Y, double Z, long Generation)> TriggerPositions = [];
        public Dictionary<IWiredContextualTrigger, (IWiredItem Box, int X, int Y, double Z, long Generation)[]> RecipientStacks = [];
        public HashSet<IWiredContextualTrigger> CancelledRecipients = [];
        public int Slots = 1;
        public bool Initialized, IsQueued;
        public LeaverWindow? LeaverWindow;
        public bool IncludeLegacy = true;
        public bool ResumeImmediately;
        /// <summary>The time its stacks fire at, when that is not the pass that runs them: the room tick that raised a walk.</summary>
        public long? FiredAt;
        public bool SamePass;
        public Modern.Actions.WiredFurniturePublication? Publication;
        public HeadingCollisionProof? CollisionProof;
        public bool Accepted, ConsumedChat;
        public WiredClickResult Click;
    }

    private sealed class LeaverWindow
    {
        public bool Dispatched;
    }

    private sealed record PendingCall(IWiredItem Source, WiredRuntimeContext Parent, WiredSelection Selection);
    private sealed record PendingSignal((Item Receiver, long Generation)[] Receivers, WiredRuntimeContext Context, WiredRuntimeContext Parent);
}
