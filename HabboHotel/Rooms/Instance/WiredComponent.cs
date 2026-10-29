using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Dapper;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Plus.Core;
using Plus.Core.Settings;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.Database;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Commands;

namespace Plus.HabboHotel.Rooms.Instance;

public partial class WiredComponent : IWiredRuntimeOperations
{
    private readonly Room _room;
    private readonly WiredStackEngine _engine;
    private readonly WiredTargetResolver _targets;
    private readonly ILogger _logger;
    private readonly TimeProvider _clock;
    private readonly IBotManagementStore _botStore;
    private readonly IGameClientManager _clients;
    private readonly IGroupManager _groups;
    private readonly IItemDataManager _definitions;
    private readonly ICommandManager _commands;
    private readonly IAccessControl _access;
    private readonly IItemTravelStore _travelStore;
    private readonly Plus.HabboHotel.Quests.IQuestManager? _quests;
    private readonly Action<WiredModernAction> _bindMovementPublication;

    public WiredComponent(Room instance, ILogger logger, TimeProvider clock, ISettingsManager settings, IWiredRoomSettingsFactory settingsFactory,
        IWiredConfigurationStore configurationStore, IDatabase database, IWiredRewardService rewardService,
        IBotManagementStore botStore, IGameClientManager clients, IGroupManager groups, IItemDataManager definitions,
        ICommandManager commands, IAccessControl access, IItemTravelStore travelStore, Plus.HabboHotel.Quests.IQuestManager? quests = null) //, RoomItem Items)
    {
        _room = instance;
        _counters = new(observeTransition: ObserveClock, gameTransition: ControlGameTimer,
            canBegin: (item, origin, actor) => _highscores?.CanBeginOwned(item, origin, actor) ?? true);
        _logger = logger;
        _clock = clock;
        _errors = new(clock);
        _configurationStore = configurationStore;
        _database = database;
        _rewards = rewardService;
        _botStore = botStore;
        _clients = clients;
        _groups = groups;
        _definitions = definitions;
        _commands = commands;
        _access = access;
        _travelStore = travelStore;
        _quests = quests;
        Settings = settingsFactory.Create(instance);
        _engine = new(
            () => (long)Stopwatch.GetElapsedTime(0).TotalMilliseconds,
            box => ReferenceEquals(_room.GetRoomItemHandler().GetItem(box.Item.Id), box.Item),
            IsActorPresent,
            OnEvent, ExceptionLogger.LogWiredException,
            WiredEngineLimits.FromSettings(settings.TryGetValue),
            CaptureActorVisit);
        _engine.ActionDue = ArmWake;
        _engine.FlashToVisits = (item, visits) => Flash(item, false, visits);
        _targets = new(
            () => _room.GetRoomItemHandler().GetFloor,
            () => _room.GetRoomUserManager().GetUserList(),
            id => _room.GetRoomItemHandler().GetItem(id),
            id => _room.GetRoomUserManager().GetRoomUserByVirtualId(id),
            () => _room.GetRoomItemHandler().GetWallAndFloor);
        _engine.BindRuntime(_room, _targets, this,
            () => _counters.HasRunning || WiredBotTargets.For(_room).HasTargets || _chests?.HasPending == true
                || WiredProjectileFlights.For(_room).HasPending || WiredDeferredStates.For(_room).HasPending, PollCounters, FlushExternalChanges);
        _engine.ObserveEvent = (evt, now) =>
        {
            _selectorState.Observe(evt, now);

            if (evt.Kind == WiredEventKind.Leave && evt.Actor != null) {
                ForgetFxActor(evt.Actor);
                WiredAvatarState.For(_room).Forget(evt.Actor);
                WiredBotTargets.For(_room).Forget(evt.Actor);
                WiredGameState.For(_room).Forget(evt.Actor);
            }
        };
        _engine.LeaverSettled = leaver =>
        {
            // A user who already came back keeps the values of the new visit.
            if (_variables?.IsValueCreated == true && _room.GetRoomUserManager()?.GetRoomUserByHabbo(leaver.HabboId) == null) {
                _variables.Value.HolderLeft(WiredVariableRuntimeFrames.UserHolder(leaver));
            }
        };
        // Only these concrete adapters are known silent for publication purposes.
        _engine.PublicationBridgesAreTrusted = true;
        _engine.PublicationEvaluationIsSilent = box => box is Plus.HabboHotel.Items.Wired.Modern.Addons.WiredAddonBox or Plus.HabboHotel.Items.Wired.Modern.Selectors.WiredSelectorBox
            or Plus.HabboHotel.Items.Wired.Modern.Addons.WiredOctoberAddonBox
            or Plus.HabboHotel.Items.Wired.Modern.Conditions.WiredModernCondition
            // This add-on only installs a deferred formatter; the message action performs the reads/output.
            || box is WiredVariableAddonBox { Descriptor.CanonicalName: "wf_xtra_text_output_variable" }
            || box.GetType() == typeof(Plus.HabboHotel.Items.Wired.Modern.Triggers.WiredModernTrigger)
            || box is Plus.HabboHotel.Items.Wired.Modern.Triggers.WiredModernTimedTrigger;
        _engine.PublicationObserverIsSilent = evt => evt.Kind != WiredEventKind.Leave;
        // This concrete flush seals from each drained snapshot before publishing its work.
        _engine.PublicationFlushIsSilent = () => true;
        _engine.PublicationPollIsSilent = () => _counterItems.Count == 0 && _chests?.HasPending != true
            && !WiredBotTargets.For(_room).HasTargets;
        _engine.LimitReached = NoteLimit;
        // The hidden message is echoed to its speaker as a whisper, after the stack's flash and its messages.
        _engine.SpeechHidden = evt =>
        {
            if (evt.Actor?.GetClient() is not { } client) {
                return;
            }

            var echo = new Plus.HabboHotel.Items.Wired.Modern.Actions.WiredChatComposer(evt.Actor.VirtualId, evt.Actor.VirtualId, evt.Message, evt.ChatStyle, -1, true);
            AfterFlash(() => client.Send(echo));
        };
        _engine.PassScope = () => _variables?.IsValueCreated == true ? _variables.Value.OpenPass() : null;
        _engine.CaptureSpeech = (context, trigger) => _variables?.IsValueCreated == true
            ? _variables.Value.CaptureSpeech(context, trigger) : null;
        _engine.ConfigurationPublished = box =>
        {
            UpdateClickEnvironment(box.Item.Id, box);
            ShowRestingState(box);

            if (_variables?.IsValueCreated == true) {
                _variables.Value.ConfigurationSaved(box);
            }

            OctoberConfigurationPublished(box);
        };
        _bindMovementPublication = _engine.CreateMovementPublicationFactory(this, DispatchWalkTransition);
    }

    private int _highscoreClockAdmission;

    private void ObserveClock(WiredClockTransition transition)
    {
        _highscores?.Observe(transition);

        // A reset of a paused game timer sends no stop of its own, so the gates open here. The gates stay locked while another Banzai or Freeze timer is started, and a reset of a timer that was never started does nothing.
        if (transition.Reason == WiredClockReason.Reset && transition.WasStarted && IsBanzaiOrFreezeTimer(transition.Item)
            && !OtherGameTimerActive(transition.Item)) {
            _room.GetGameManager().UnlockGates();
        }
    }

    private static bool IsBanzaiOrFreezeTimer(Item item) => WiredCounterController.IsGameTimer(item)
        && (item.Definition.InteractionType is InteractionType.Banzaicounter or InteractionType.Freezetimer
            || item.Definition.ItemName is "bb_counter" or "es_counter");

    // The Banzai and Freeze gates lock when the timer starts and stay locked while it is paused. They open when the countdown ends or the timer is reset.
    private bool KeepsGatesLocked(WiredClockTransition transition) =>
        transition.Reason is WiredClockReason.Stop or WiredClockReason.Pause || OtherGameTimerActive(transition.Item);

    private bool OtherGameTimerActive(Item item) => _counters.HasOtherActiveCountdown(item, IsBanzaiOrFreezeTimer);

    private void ControlGameTimer(WiredClockTransition transition)
    {
        var item = transition.Item;
        var start = transition.Reason is WiredClockReason.Start or WiredClockReason.Resume;
        var type = item.Definition.InteractionType;
        var banzai = type == InteractionType.Banzaicounter || item.Definition.ItemName == "bb_counter";
        var freeze = type == InteractionType.Freezetimer || item.Definition.ItemName == "es_counter";

        _highscoreClockAdmission++;

        try {
            if (!WiredCounterController.IsGameTimer(item)) {
                // A Wired game counter is the game itself: its Start resets the team scores and raises Game Starts, its Stop raises Game Ends.
                if (start) {
                    _room.GetGameManager().Reset();
                }

                TriggerEvent(start ? WiredBoxType.TriggerGameStarts : WiredBoxType.TriggerGameEnds, null);

                return;
            }

            if (start) {
                // Banzai and Freeze zero the scores in their own start, in one update with the field. A second reset would send it twice.
                if (banzai || freeze) {
                    _room.GetGameManager().Highscores.Invalidate();
                }
                else {
                    _room.GetGameManager().Reset();
                }

                _highscores?.Start(transition);

                if (banzai) {
                    _room.GetBanzai().BanzaiStart();
                }
                else {
                    if (freeze) {
                        _room.GetFreeze().StartGame();
                    }
                    else {
                        _room.GetSoccer().StartGame();
                    }

                    TriggerEvent(WiredBoxType.TriggerGameStarts, null);
                }
            }
            else if (banzai) {
                _room.GetBanzai().BanzaiEnd(keepGatesLocked: KeepsGatesLocked(transition));
            }
            else if (freeze) {
                _room.GetFreeze().StopGame(keepGatesLocked: KeepsGatesLocked(transition));
            }
            else {
                _room.GetSoccer().StopGame();
            }
        }
        finally {
            _highscoreClockAdmission--;
        }
    }

    public void OnCycle()
    {
        ShowLinkArrivals();
        WiredAvatarState.For(_room).FlushTickEffects();
        _highscores?.OnOwnedPass();

        // The blobs change first, so that the state writes are reported in this same pass. A game counter start resets them before Toggle State changes its own.
        foreach (var blob in WiredDeferredStates.For(_room).TakeBlobsToReset()) {
            blob.LegacyDataString = "0";
            PublishState(blob, true);
        }

        foreach (var blob in WiredDeferredStates.For(_room).TakeBlobs()) {
            blob.LegacyDataString = WiredDeferredStates.NextState(blob);
            PublishState(blob, true);
        }

        PublishStateWrites();

        _engine.OnCycle();
        FlushVariableFx();
        FlushFlashes();
        FlushLeaderboards();
        FlushLeaderboardRepublish();
        ResendRestackedSteps();
    }

    private readonly HashSet<long> _wakeDue = [];
    private const long WakeLeadMs = 3;

    /// <summary>Calls the action after the given ms. Tests replace it to see which wake-ups are armed.</summary>
    internal Action<double, Action> WakeAfter { get; set; } = WiredWakeClock.Shared.After;

    /// <summary>The threads that run this room's wake-up passes. Tests give a room its own, so passes of other rooms cannot hold them.</summary>
    internal WiredWakeWorkers Workers { get; set; } = WiredWakeWorkers.Shared;

    /// <summary>Runs a link delivery to another room off the sending room's lock. Tests give a room a thread of its own, so a busy thread pool cannot delay it.</summary>
    internal Action<Action> HandOff { get; set; } = work => _ = Task.Run(work);

    private void ArmWake(long dueMs)
    {
        // The room tick runs what falls due on it, after its UserUpdate. A delay that ends on a room tick goes out in the ms of that tick's UserUpdate, after it. A wake-up pass could run first.
        if (dueMs % WiredFlashStates.StateUpdateMilliseconds == 0) {
            return;
        }

        lock (_wakeDue) {
            if (!_wakeDue.Add(dueMs)) {
                return;
            }
        }

        // Handed over a little early; the worker holds the pass until the due time.
        WakeAfter(dueMs - _engine.Now - WakeLeadMs, () => Wake(dueMs));
    }

    // A delayed effect's pass runs at its due time. When a pass is already running it may have started before the time fell due, so the
    // wake-up retries each millisecond for a few, then leaves it to the next 50 ms step.
    private void Wake(long dueMs)
    {
        // A queued pool item, or a new thread, can wait several ms to start on a busy server, which the effect would land late by; the
        // wake-up hands the pass to a pre-started worker and the clock stays free.
        var startAt = WiredWakeClock.Now + Math.Clamp(dueMs - _engine.Now, 0, WakeLeadMs + 1);

        StartWakePass(startAt, 0, () =>
        {
            lock (_wakeDue) {
                _wakeDue.Remove(dueMs);
            }
        });
    }

    /// <summary>Queues an event from another room and starts a Wired pass of this room now, instead of at its next 50 ms step.</summary>
    internal void DispatchNow(WiredRuntimeEvent @event)
    {
        _linkArrivals.Enqueue(@event);
        StartWakePass(WiredWakeClock.Now, 0, null, 1000, () => !_linkArrivals.IsEmpty);
    }

    private void StartWakePass(double startAtMs, int attempt, Action? done = null, int maxAttempts = 10, Func<bool>? pending = null)
    {
        if (_room.MDisposed || _room.IsCrashed || pending?.Invoke() == false) {
            return;
        }

        if (RoomCycle.TryStart(_room, _room.ProcessWiredOnly, workers: Workers, startAtMs: startAtMs) || attempt >= maxAttempts) {
            done?.Invoke();

            return;
        }

        WakeAfter(1, () => StartWakePass(startAtMs, attempt + 1, done, maxAttempts, pending));
    }

    internal void OnFastCycle()
    {
        ShowLinkArrivals();
        ForgetRestackedSteps();
        _highscores?.OnOwnedPass();
        PublishStateWrites();
        _engine.OnFastCycle();

        if (_variables?.IsValueCreated == true && _variables.Value.FxDirty) {
            FlushVariableFx();
        }

        FlushFlashes();
    }
    /// <summary>A Wired game counter is running, which is a game in progress for scores and team games.</summary>
    public bool GameCounterRunning => _counters.HasRunningGameCounter;
    internal bool NeedsFastCycle => _engine.NeedsFastCycle;
    internal long RoomTickIndex => _engine.RoomTickIndex;
    internal object? ExecutingFiring => _engine.ExecutingFiring;
    internal void ObserveFastWork(Action<bool>? observer) => _engine.ObserveFastWork(observer);
    /// <summary>Drops in-room user values left from an earlier visit, so nothing comes back on return.</summary>
    public void VisitorArriving(int habboId) => _engine.Mutate(() =>
    {
        if (_variables?.IsValueCreated == true) {
            _variables.Value.Module.HolderLeft(new(WiredVariableTarget.User, habboId, 0));
        }

        return true;
    });
    public bool Dispatch(WiredRuntimeEvent @event)
    {
        @event = @event with { Visits = _room.GetRoomUserManager()?.Visits ?? int.MaxValue };
        var ranAtOnce = @event.Kind is WiredEventKind.Speech or WiredEventKind.ClickFurni;
        var holdsMovements = @event.Kind == WiredEventKind.ClickFurni;
        bool dispatched;

        if (holdsMovements) {
            Interlocked.Increment(ref _movementHolds);
        }

        try {
            if (@event.Kind == WiredEventKind.StateChanged) {
                (ranAtOnce, dispatched) = _engine.DispatchStateChange(@event);
            }
            else {
                dispatched = WithRoomOwner(ranAtOnce, () => _engine.Mutate(() => DispatchCore(@event)));
            }
        }
        finally {
            if (holdsMovements) {
                Interlocked.Decrement(ref _movementHolds);
            }
        }

        // Speech and a furni click run their stacks at once: send what they flashed and wrote now, with the chat.
        // The flash arrives in the same batch as the chat that fired it. A furni a user switches flashes its Stuff State stack in the millisecond of its new state, right after it.
        if (ranAtOnce) {
            FlushSpeechOutput();
        }

        return dispatched;
    }

    private bool DispatchCore(WiredRuntimeEvent @event)
    {
        if (_highscoreClockAdmission == 0 && @event.Kind is WiredEventKind.GameStart or WiredEventKind.GameEnd) {
            _highscores?.Invalidate();
        }

        if (@event.Kind == WiredEventKind.GameStart) {
            WiredGameState.For(_room).ResetQuotas();
        }
        else if (@event.Kind == WiredEventKind.GameEnd) {
            _counters.OnGameEnded();
        }

        return @event.Kind switch
        {
            WiredEventKind.Speech => _engine.DispatchSynchronously(@event),
            // A click is answered as fast as a say or a furni use, and the boxes of every stack it passed and the trigger of every stack it failed flash in one ObjectsDataUpdate, before the chat of the passing stacks.
            WiredEventKind.ClickFurni => _engine.DispatchAtOnce(@event),
            // The Room Initialized stacks have run before the first entrant is sent the room.
            WiredEventKind.RoomInitialized => _engine.DispatchOnLoad(@event),
            _ => _engine.Enqueue(@event)
        };
    }
    public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) =>
        _engine.CallStacks(context, targets, negative);
    public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) =>
        _engine.SendSignal(context, receivers, selection, negative);
    public bool ScheduleOnRoomTick(WiredRuntimeContext context, long tickBoundary, Action callback, Action? onCancelled = null,
        bool afterAvatarUpdates = false) =>
        _engine.ScheduleOnRoomTick(context, tickBoundary, callback, onCancelled, afterAvatarUpdates);
    public long FirstRoomTickAfter(long firing) => _engine.FirstRoomTickAfter(firing);

    /// <summary>Called at the start of every room tick's pass, before the room moves and serializes avatars.</summary>
    public void OnRoomTick(bool splitEnd = false) => _engine.OnRoomTick(splitEnd);

    public void OnRoomTickEnd()
    {
        ForgetRestackedSteps();
        _engine.OnRoomTickEnd();
    }

    public bool ScheduleAux(WiredRuntimeContext context, int delayMilliseconds, Action callback, Action? onCancelled = null,
        bool alignToStateUpdate = false) =>
        _engine.ScheduleAux(context, delayMilliseconds, callback, onCancelled, alignToStateUpdate);
    public void DispatchWalkTransition(RoomUser actor, IEnumerable<Item> before, IEnumerable<Item> after)
    {
        var oldItems = before.DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var newItems = after.DistinctBy(x => x.Id).ToDictionary(x => x.Id);

        foreach (var item in oldItems.Values.Where(x => !newItems.ContainsKey(x.Id)).OrderBy(x => x.GetZ).ThenBy(x => x.Id)) {
            if (ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)) {
                item.UserWalksOffFurni(actor);
            }
        }

        foreach (var item in newItems.Values.Where(x => !oldItems.ContainsKey(x.Id)).OrderBy(x => x.GetZ).ThenBy(x => x.Id)) {
            if (ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)) {
                item.UserWalksOnFurni(actor);
            }
        }
    }
    public void ResetTimers(IEnumerable<Item> targets) => _engine.ResetTimers(targets);
    public bool PublishConfigured(IWiredConfiguredItem original, WiredConfiguration validated, Action persistValidated) =>
        _engine.PublishConfigured(original, validated, persistValidated);

    internal bool IsActorPresent(object[] arguments) => arguments.Length == 0 || arguments[0] is not Habbo player
        || player.InRoom && ReferenceEquals(player.CurrentRoom, _room);

    internal object? CaptureActorVisit(object[] arguments) => arguments.Length > 0 && arguments[0] is Habbo player
        ? _room.GetRoomUserManager()?.GetRoomUserByHabbo(player.Id) : null;

    public bool RunStack(IWiredItem source, params object[] arguments) => _engine.RunStack(source, arguments);

    internal bool RunStack(IWiredItem source, object[] arguments, Action onAccepted) =>
        _engine.RunStack(source, arguments, onAccepted);

    internal bool RunPeriodicStack(IWiredItem source, object[][] actors) => _engine.RunPeriodicStack(source, actors);

    internal bool CallStacks(IEnumerable<Item> targets, params object[] arguments) => _engine.CallStacks(targets, arguments);

    public IWiredItem? LoadWiredBox(Item item)
    {
        if (DescriptorOf(item) is not { } descriptor || CreateConfiguredBox(item, descriptor) is not { } configured) {
            _logger.LogWarning("Unsupported wired type {WiredType} on item {ItemId} in room {RoomId}",
                item.Definition.WiredType, item.Id, _room.Id);

            return null;
        }

        try {
            var saved = ConfigurationStore.Load(item.Id, descriptor);

            // A card without a saved row keeps its compiled defaults and gains persistence only on a successful save.
            if (saved != null) {
                if (saved.SelectedItems.Concat(saved.SecondarySelectedItems)
                    .Any(id => _room.GetRoomItemHandler().GetItem(id)?.IsTemporary == true)) {
                    throw new InvalidDataException("Saved static selections cannot reference temporary room furniture.");
                }

                saved = WithRoomPicks(item, descriptor, saved);

                if (!configured.TryValidateConfiguration(saved, out var validated, out var error)) {
                    throw new InvalidDataException(error);
                }

                configured.ApplyConfiguration(validated);
            }

            if (_variables?.IsValueCreated == true) {
                _variables.Value.ConfigurationLoaded(configured);
            }

            return AddBox(configured) ? configured : null;
        }
        catch (Exception error) {
            _logger.LogError(error, "Cannot load Wired configuration for item {ItemId} in room {RoomId}; saved bytes retained: {Reason}",
                item.Id, _room.Id, error.Message);

            return null;
        }
    }

    /// <summary>
    /// A saved card's picks as this room holds them. Furni that left the room drops out of the selection rather than failing the card,
    /// and a pick read from a schema 1 row, which did not record wall picks, takes the wall flag of the furni it names. The saved row
    /// keeps its picks until the card is saved again.
    /// </summary>
    private WiredConfiguration WithRoomPicks(Item item, WiredBoxDescriptor descriptor, WiredConfiguration saved)
    {
        if (saved.Origin?.Native is not { } native) {
            return saved;
        }

        var handler = _room.GetRoomItemHandler();
        ImmutableArray<WiredNativeItemReference> InRoom(ImmutableArray<WiredNativeItemReference> picks) =>
            [.. picks.Select(pick => handler.GetItem(pick.ItemId) is { } furni ? pick with { Wall = furni.IsWallItem } : null).OfType<WiredNativeItemReference>()];
        var primary = InRoom(native.PrimaryItems);
        var secondary = InRoom(native.SecondaryItems);

        if (primary.SequenceEqual(native.PrimaryItems) && secondary.SequenceEqual(native.SecondaryItems)) {
            return saved;
        }

        var placed = native with { PrimaryItems = primary, SecondaryItems = secondary };

        return WiredNativeEditorProjection.TryCompile(item.Id, descriptor, placed, out var runtime, storedRow: true) ? runtime : saved;
    }

    /// <summary>The registry descriptor of placed furniture: named by its definition, or by its legacy wired type.</summary>
    internal static WiredBoxDescriptor? DescriptorOf(Item item) => item.Definition.WiredDescriptor
        ?? (WiredBoxRegistry.TryGetByLegacyType(item.Definition.WiredType, out var descriptor) ? descriptor : null);

    public bool IsTrigger(Item item) => item.Definition.InteractionType == InteractionType.WiredTrigger;

    public bool IsEffect(Item item) => item.Definition.InteractionType == InteractionType.WiredEffect;

    public bool IsCondition(Item item) => item.Definition.InteractionType == InteractionType.WiredCondition;

    public bool OtherBoxHasItem(IWiredItem box, uint itemId)
    {
        if (box == null) {
            return false;
        }

        ICollection<IWiredItem> items = GetEffects(box).Where(x => x.Item.Id != box.Item.Id).ToList();

        if (items != null && items.Count > 0) {
            foreach (var item in items) {
                if (item.Type != WiredBoxType.EffectMoveAndRotate && item.Type != WiredBoxType.EffectMoveFurniFromNearestUser && item.Type != WiredBoxType.EffectMoveFurniToNearestUser) {
                    continue;
                }

                if (item.SetItems == null || item.SetItems.Count == 0) {
                    continue;
                }

                if (item.SetItems.ContainsKey(itemId)) {
                    return true;
                }
            }
        }

        return false;
    }

    public bool TriggerEvent(WiredBoxType type, params object[] arguments)
    {
        var triggered = WithRoomOwner(type == WiredBoxType.TriggerUserSays, () => TriggerEventCore(type, arguments));

        // A chat runs its stacks at once, like Dispatch of speech: their flashes go out with the chat. Enter Room stacks run inside the
        // room entry, so their flashes go out with it.
        // The flashes of a say with Variable Changed stacks wait for them (see FinishSpeech), so the whole cascade is one batch.
        if (type is WiredBoxType.TriggerUserSays && !_engine.HasHeldVariableStacks) {
            FlushSpeechOutput();
        }
        else if (type is WiredBoxType.TriggerRoomEnter) {
            FlushFlashes();
        }

        return triggered;
    }

    // Variable Changed stacks raised by a say run after its echo.
    public void FinishSpeech()
    {
        ReleaseAfterEcho();
        _engine.DrainVariableStacks();
        FlushSpeechOutput();
    }

    // A variable a say gave goes out as its Variable FX status in the same tick, before the packet that flashes the boxes of the stack.
    private void FlushSpeechOutput()
    {
        if (_variables?.IsValueCreated == true && _variables.Value.FxDirty) {
            FlushVariableFx();
        }

        FlushFlashes();
    }

    private bool TriggerEventCore(WiredBoxType type, object[] arguments)
    {
        arguments ??= [];

        WiredEventKind? kind = type switch
        {
            WiredBoxType.TriggerRoomEnter => WiredEventKind.Enter,
            WiredBoxType.TriggerUserSays => WiredEventKind.Speech,
            WiredBoxType.TriggerWalkOnFurni => WiredEventKind.WalkOn,
            WiredBoxType.TriggerWalkOffFurni => WiredEventKind.WalkOff,
            WiredBoxType.TriggerStateChanges => WiredEventKind.Use,
            WiredBoxType.TriggerGameStarts => WiredEventKind.GameStart,
            WiredBoxType.TriggerGameEnds => WiredEventKind.GameEnd,
            WiredBoxType.TriggerUserFurniCollision => WiredEventKind.Collision,
            _ => null
        };
        var actor = arguments.OfType<RoomUser>().FirstOrDefault()
            ?? (arguments.OfType<Habbo>().FirstOrDefault() is { } habbo
                ? _room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) : null);
        var typed = kind == null ? null : new WiredRuntimeEvent(kind.Value)
        {
            Actor = actor,
            EventItem = arguments.OfType<Item>().FirstOrDefault(),
            Message = kind == WiredEventKind.Speech ? arguments.OfType<string>().FirstOrDefault() ?? "" : "",
            ChatStyle = kind == WiredEventKind.Speech ? arguments.OfType<int>().FirstOrDefault() : 0,
            ChatType = kind == WiredEventKind.Speech && arguments.OfType<bool>().FirstOrDefault() ? 1 : 0
        };

        if (kind is WiredEventKind.GameStart or WiredEventKind.GameEnd) {
            return _engine.Mutate(() =>
            {
                if (_highscoreClockAdmission == 0) {
                    _highscores?.Invalidate();
                }

                if (kind == WiredEventKind.GameStart) {
                    WiredGameState.For(_room).ResetQuotas();
                }
                else {
                    _counters.OnGameEnded();
                }

                return _engine.DispatchLegacy(type, typed, arguments);
            });
        }

        return _engine.DispatchLegacy(type, typed, arguments);
    }

    public ICollection<IWiredItem> GetTriggers(IWiredItem item) => _engine.GetBoxes(item, InteractionType.WiredTrigger);

    public ICollection<IWiredItem> GetEffects(IWiredItem item) => _engine.GetBoxes(item, InteractionType.WiredEffect);

    public IWiredItem? GetRandomEffect(ICollection<IWiredItem> effects)
    {
        return effects.OrderBy(x => Guid.NewGuid()).FirstOrDefault();
    }

    public bool OnUserFurniCollision(Room room, Item item)
    {
        if (room == null || item == null) {
            return false;
        }

        foreach (var point in item.GetSides()) {
            if (room.GetGameMap().SquareHasUsers(point.X, point.Y)) {
                var users = room.GetGameMap().GetRoomUsers(point);

                if (users != null && users.Count > 0) {
                    foreach (var user in users.ToList()) {
                        if (user == null) {
                            continue;
                        }

                        item.UserFurniCollision(user);
                    }
                }
                else {
                    continue;
                }
            }
            else {
                continue;
            }
        }

        return true;
    }

    public ICollection<IWiredItem> GetConditions(IWiredItem item) => _engine.GetBoxes(item, InteractionType.WiredCondition);

    /// <summary>
    /// A box acts: it flips to its other flash state, at most once per 500 ms state-update window; boxes hold their state
    /// between flashes. A signal antenna (not a box) flips on every signal.
    /// Under a 200 ms repeater every box of both stacks flips 600, 400, 600, 400 ms apart while the antenna flips every 200 ms; the flips share one ObjectsDataUpdate per tick.
    /// </summary>
    public void OnEvent(Item item) => Flash(item, false);

    /// <param name="flipAgain">The box flips again although it flashed in this half second (Toggle State on a box of its own stack).</param>
    /// <param name="visits">Only users whose <see cref="RoomUser.Visit"/> is at most this see the flash (<see cref="WiredRuntimeEvent.Visits"/>).
    /// A user who leaves and enters again gets no box flash for its own leave.</param>
    internal void Flash(Item item, bool flipAgain, int visits = int.MaxValue)
    {
        // Hidden wired boxes are not on the clients, so they do not flash.
        if (_room.HideWired) {
            return;
        }

        TryGet(item.Id, out var box);

        if (box != null) {
            var window = _engine.Now / WiredFlashStates.StateUpdateMilliseconds;

            lock (_flashWindows) {
                if (!flipAgain && _flashWindows.TryGetValue(item.Id, out var last) && last == window) {
                    return;
                }

                _flashWindows[item.Id] = window;
            }
        }

        item.LegacyDataString = WiredFlashStates.Next(item.LegacyDataString, box);

        lock (_pendingFlashes) {
            // A flash already in the batch keeps the wider audience.
            if (_pendingFlashes.Add(item) ? visits < int.MaxValue : _flashVisits.ContainsKey(item)) {
                _flashVisits[item] = Math.Max(visits, _flashVisits.GetValueOrDefault(item));
            }
        }
    }

    /// <summary>
    /// A variable definition shows the kind of its write in the next flash batch. Within one stack firing only the first write in the batch counts: give 5 and then add 3 in one firing shows 6 (7 on the next firing), not 2.
    /// </summary>
    internal void FlashDefinition(Item item, int kind, object? firing)
    {
        if (_room.HideWired) {
            return;
        }

        lock (_pendingFlashes) {
            if (firing != null && !_definitionFirings.Add((item.Id, firing))) {
                return;
            }

            item.LegacyDataString = WiredFlashStates.NextDefinition(item.LegacyDataString, kind);
            _pendingFlashes.Add(item);
        }
    }

    /// <summary>
    /// A furni state written by wired goes out in the tick's data batch, with the antenna flips and any due box flashes, on every wired tick and ahead of that tick's movement packet.
    /// Other data formats keep the object update. A furni whose height depends on its state first gets its ObjectUpdate with the new height and the height map, and is listed in the batch too.
    /// </summary>
    internal void PublishState(Item item, bool persist)
    {
        if (!item.IsFloorItem || item.ExtraData is not Plus.HabboHotel.Items.DataFormat.LegacyDataFormat) {
            item.UpdateState(persist, true);

            return;
        }

        item.UpdateState(persist, item.Definition.AdjustableHeights.Count > 1);
        Plus.HabboHotel.Items.ConfigurationItemStates.StateChanged(item);

        lock (_pendingFlashes) {
            _pendingFlashes.Add(item);
        }
    }

    /// <summary>
    /// A counter value written by Wired (Start reset, Reset, Adjust Clock) goes out in the same ObjectsDataUpdate as the event's flashes, each counter once with its last value. Other data formats keep their own frame.
    /// </summary>
    internal bool TryQueueBatchData(Item item)
    {
        if (!item.IsFloorItem || item.ExtraData is not Plus.HabboHotel.Items.DataFormat.LegacyDataFormat) {
            return false;
        }

        lock (_pendingFlashes) {
            _pendingFlashes.Add(item);
        }

        return true;
    }

    /// <summary>
    /// Every save of a selector sends its resting state: 3 (green) with "Filter existing selection", 0 (orange) without; Invert is not
    /// a colour. ObjectDataUpdate goes right after UpdateSelector. Room entry lists the last state, flashes included.
    /// </summary>
    private void ShowRestingState(IWiredItem box)
    {
        var state = WiredFlashStates.RestingState(box)?.ToString(System.Globalization.CultureInfo.InvariantCulture);

        if (state == null) {
            return;
        }

        box.Item.LegacyDataString = state;
        box.Item.UpdateState(true, false);

        // A single ObjectDataUpdate. Hidden boxes are not on the clients; they show the state when they come back.
        if (!_room.HideWired) {
            _room.SendObjectData(box.Item, state, false);
        }
    }

    private readonly HashSet<Item> _pendingFlashes = [];
    private readonly List<Item> _pendingObjectUpdates = [];
    private readonly HashSet<Item> _changedInPlace = [];
    private readonly HashSet<Item> _changedTile = [];

    /// <summary>
    /// Holds the ObjectUpdate of a furni that a firing moved without WiredMovements. <see cref="FlushFlashData"/> sends it first, with the state the furni ends on.
    /// With No Move Animation, a furni that changes again on its own tile in the same firing, having never left it, is sent as a WiredMovements entry instead.
    /// The method then returns true and the caller sends that entry.
    /// </summary>
    internal bool DeferObjectUpdate(Item item, bool onItsTile, bool noAnimation)
    {
        lock (_pendingFlashes) {
            if (!onItsTile) {
                _changedInPlace.Remove(item);
                _changedTile.Add(item);
            }
            else if (_changedInPlace.Contains(item) && noAnimation) {
                _pendingObjectUpdates.Remove(item);

                return true;
            }
            else if (!_changedTile.Contains(item)) {
                _changedInPlace.Add(item);
            }

            if (!_pendingObjectUpdates.Contains(item)) {
                _pendingObjectUpdates.Add(item);
            }

            return false;
        }
    }

    private object? _tileChangeFiring;
    private readonly HashSet<uint> _tileChanged = [];

    /// <summary>Records that a furni changed tile in this firing.</summary>
    internal void NoteTileChange(object? firing, Item item)
    {
        if (firing == null) {
            return;
        }

        lock (_pendingFlashes) {
            if (!ReferenceEquals(_tileChangeFiring, firing)) {
                _tileChangeFiring = firing;
                _tileChanged.Clear();
            }

            _tileChanged.Add(item.Id);
        }
    }

    /// <summary>The furni changed tile earlier in this same firing.</summary>
    internal bool EnteredTileIn(object? firing, Item item)
    {
        lock (_pendingFlashes) {
            return firing != null && ReferenceEquals(_tileChangeFiring, firing) && _tileChanged.Contains(item.Id);
        }
    }

    private void SendDeferredObjectUpdates()
    {
        Item[] items;

        lock (_pendingFlashes) {
            _changedInPlace.Clear();
            _changedTile.Clear();

            if (_pendingObjectUpdates.Count == 0) {
                return;
            }

            items = [.. _pendingObjectUpdates];
            _pendingObjectUpdates.Clear();
        }

        foreach (var item in items) {
            if (!ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item) || FurniVisibility.IsConcealed(_room, item)) {
                continue;
            }

            var snapshot = RoomItemSnapshot.Capture(item);
            _room.SendPacket(new ObjectUpdateComposer(snapshot));
            _room.SendPacket(new FurnitureMetadataComposer([snapshot]));
        }
    }

    private readonly Dictionary<Item, int> _flashVisits = [];
    private readonly HashSet<(uint ItemId, object Firing)> _definitionFirings = [];
    private readonly Dictionary<uint, long> _flashWindows = [];

    /// <summary>
    /// Sends the flashed boxes' states and Wired's furni state writes together, at the end of the pass that made them. Each box flips at most
    /// once per 500 ms window (see <see cref="OnEvent"/>); the room has no shared flash interval. A box flash goes out in the same batch as the chat that fired it, and two stacks fired 200 ms apart
    /// flash 200 ms apart.
    /// </summary>
    internal void FlushFlashes()
    {
        FlushFlashData();

        Action[] messages;

        lock (_pendingFlashes) {
            messages = [.. _afterFlash];
            _afterFlash.Clear();
        }

        foreach (var send in messages) {
            send();
        }
    }

    /// <summary>
    /// Sends only the ObjectsDataUpdate of <see cref="FlushFlashes"/> and keeps the held messages. The ObjectsDataUpdate and the WiredMovements of one firing are adjacent, so a movement flush sends the messages after its WiredMovements.
    /// </summary>
    internal void FlushFlashData()
    {
        SendDeferredObjectUpdates();

        Item[] items;
        Dictionary<Item, int> visits;
        IServerPacket[] movements;

        lock (_pendingFlashes) {
            if (_pendingFlashes.Count == 0 && _afterFlash.Count == 0 && _heldMovements.Count == 0) {
                return;
            }

            movements = [.. _heldMovements];
            _heldMovements.Clear();

            items = [.. _pendingFlashes.Where(item => ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item))];
            _pendingFlashes.Clear();
            visits = new(_flashVisits);
            _flashVisits.Clear();
            // The first-write rule of FlashDefinition covers one batch, so a flush in the middle of a firing (such as before a
            // movement) lets that firing's next write show again.
            _definitionFirings.Clear();
        }

        if (items.Length > 0) {
            _room.SendObjectsData(FlashOrder(items), visits);
        }

        foreach (var movement in movements) {
            _room.SendPacket(movement);
        }
    }

    private readonly List<IServerPacket> _heldMovements = [];
    private int _movementHolds;
    internal bool HoldsMovements => _movementHolds > 0;

    /// <summary>
    /// Sends a WiredMovements after the pending ObjectsDataUpdate. While a click runs its stacks, it waits for the end of the click, so
    /// the states that the click's other effects write still go out before it. A click whose stack toggles furni and turns the user sends one ObjectsDataUpdate with the flashes and the new states, then the
    /// WiredMovements, whichever effect runs first.
    /// </summary>
    internal void SendWiredMovement(IServerPacket packet)
    {
        if (_movementHolds > 0) {
            lock (_pendingFlashes) {
                _heldMovements.Add(packet);
            }

            return;
        }

        FlushFlashData();
        _room.SendPacket(packet);
    }

    private readonly List<Action> _afterFlash = [];
    private readonly List<Action> _afterEcho = [];

    /// <summary>
    /// Like <see cref="AfterFlash(Action)"/>, but a message from a stack that a Send Signal reached during a say waits for the echo of the
    /// say. It comes after the echo, in the same millisecond. <see cref="FinishSpeech"/> sends it.
    /// </summary>
    internal void AfterFlash(Action send, WiredRuntimeContext context)
    {
        if (!_engine.OutputFollowsEcho(context)) {
            AfterFlash(send);

            return;
        }

        lock (_pendingFlashes) {
            _afterEcho.Add(send);
        }
    }

    // The held messages join the pass's flash, so they still follow the flash batch like every other stack message.
    private void ReleaseAfterEcho()
    {
        lock (_pendingFlashes) {
            _afterFlash.AddRange(_afterEcho);
            _afterEcho.Clear();
        }
    }

    /// <summary>
    /// Sends a stack's chat output after the pass's flash: the flash comes before the message a stack shows (and before the speaker's chat), while packets such as YouArePlayingGame or AvatarEffect go out at once.
    /// </summary>
    internal void AfterFlash(Action send)
    {
        lock (_pendingFlashes) {
            _afterFlash.Add(send);
        }
    }

    /// <summary>
    /// The tick's data batch lists the furni states written by Wired first, then the trigger box, then the stack's other boxes (walks-on [trigger, show_message]; periodically [colour tile, trigger, toggle_state]).
    /// </summary>
    internal static Item[] FlashOrder(IEnumerable<Item> items) => [.. items.OrderBy(item => !item.IsWired ? 0
        : item.Definition.InteractionType == InteractionType.WiredTrigger ? 1 : 2)];

    public bool AddBox(IWiredItem item) => _engine.Mutate(() =>
    {
        if (!_engine.Add(item)) {
            return false;
        }

        UpdateClickEnvironment(item.Item.Id, item);

        return true;
    });

    public bool TryRemove(uint itemId) => _engine.Mutate(() =>
    {
        if (!_engine.Remove(itemId)) {
            return false;
        }

        UpdateClickEnvironment(itemId);

        return true;
    });

    public bool TryGet(uint id, [NotNullWhen(true)] out IWiredItem? item) => _engine.TryGet(id, out item);

    public void Cleanup()
    {
        lock (_flashWindows) {
            _flashWindows.Clear();
        }

        WiredTemporaryEffects.For(_room).Clear();
        _engine.Clear();
        _selectorState.Reset();
        WiredBotTargets.For(_room).Clear();
        WiredProjectileFlights.For(_room).Clear();
        WiredGameState.For(_room).Clear();
        WiredAvatarState.For(_room).Clear();
        _counters.Clear();
        _counterItems.Clear();
        _clickUserTriggers.Clear();
        _fxViewers.Clear();
        _roomLog.Clear();
        ClearStateWrites();
    }
}
