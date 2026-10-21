using Plus.HabboHotel.Items;
using Microsoft.Extensions.Logging;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.Database;
using Plus.HabboHotel.Items.Wired.Chests;

namespace Plus.HabboHotel.Rooms.Instance;

public partial class WiredComponent
{
    private readonly WiredSelectorRoomState _selectorState = new();
    private readonly WiredCounterController _counters;
    private readonly WiredRoomLog _roomLog = new();
    private readonly WiredErrorLog _errors;
    // The monitor polls several times a second; the full log stays on the paged log request.
    private const int MonitorHistory = 100;
    private readonly Dictionary<uint, Item> _counterItems = [];
    private readonly IWiredConfigurationStore _configurationStore;
    private readonly IDatabase _database;
    private readonly IWiredRewardService _rewards;
    private Lazy<WiredRoomVariables>? _variables;
    private WiredChestRoom? _chests;
    private Plus.HabboHotel.Rooms.Games.RoomHighscores? _highscores;
    internal void InitializeHighscores(Plus.HabboHotel.Rooms.Games.IRoomHighscoreStore? store = null)
    {
        var producer = _room.GetGameManager().Highscores;
        producer.Initialize(store ?? new Plus.HabboHotel.Rooms.Games.RoomHighscoreStore(_database));
        Volatile.Write(ref _highscores, producer);
    }
    private bool WithHighscoreSpeechOwner(bool speech, Func<bool> action)
    {
        // Speech runs its stacks inside an owned pass, so avatar moves land at once instead of on the next room tick. Observed on AIR
        // WIN63-202610151745-318700621: Move/Rotate User and User To Furni move the avatar with the say (+130 ms round trip on all
        // 13 fires), while ours waited for the room tick (+480..+500 ms).
        if (!speech || Plus.HabboHotel.Rooms.PathFinding.RoomOwnerScope.IsOwner(_room) || _engine.CurrentThreadOwnsPass) {
            return action();
        }

        var completed = false;
        var result = false;
        _room.RunFastPass(() => { result = action(); completed = true; });

        return completed && result; // A skipped/failed pass is never retried as a second admission.
    }
    public WiredChestRoom Chests => _chests ??= new(_room, new DatabaseWiredChestStore(_database, _definitions), _clock, evt => Dispatch(evt));
    public void WithChests(Action<WiredChestRoom> action) => _engine.Mutate(() => { action(Chests); return true; });
    public WiredRoomSettings Settings { get; }
    private TimeZoneInfo EffectiveTimeZone => Settings.ExplicitTimeZone ?? WiredRoomSettings.DefaultTimeZone;
    internal DateTimeOffset CalendarTime =>
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), EffectiveTimeZone);
    /// <summary>
    /// The clock Date Matches and Time Matches read. Observed on AIR WIN63-202610201032-107964540: a condition without a timezone of its own
    /// reads UTC, not the zone that @current_time uses.
    /// </summary>
    internal DateTimeOffset ConditionCalendarTime =>
        TimeZoneInfo.ConvertTime(_clock.GetUtcNow(), Settings.ExplicitTimeZone ?? TimeZoneInfo.Utc);
    private IWiredConfigurationStore ConfigurationStore => _configurationStore;
    public WiredRoomVariables Variables => (_variables ??= new(() => new(_room,
        _database, _clock, builtinRead: ReadBuiltin, builtinWrite: WriteBuiltin, stateChanged: PublishBuiltinStateChanged, travelStore: _travelStore, quests: _quests)
    { TimeZone = () => EffectiveTimeZone })).Value;

    // Returns a detached concrete candidate. Registration and persistence belong to the loader/publisher.
    public IWiredConfiguredItem? CreateConfiguredBox(Item item, WiredBoxDescriptor? descriptor = null)
    {
        // Plus command boxes retain CommandManager dispatch and its feedback; speech is a different behavior.
        if (item.Definition.WiredType == WiredBoxType.TriggerUserSaysCommand) {
            return null;
        }

        descriptor ??= DescriptorOf(item);

        if (descriptor == null) {
            return null;
        }

        IWiredConfiguredItem? box = null;
        WiredConfiguration? defaults = null;

        if (WiredChestBox.Names.Contains(descriptor.CanonicalName)) {
            box = WiredChestBox.Create(_room, item, descriptor, Chests);
        }
        else if (descriptor.Category == WiredBoxCategory.Selector && WiredSelectorModule.Names.Contains(descriptor.CanonicalName)) {
            box = new WiredSelectorBox(_room, item, descriptor, _selectorState, _groups,
                context => WiredSelectorVariableBridge.Create(context, Variables.Module));
        }
        else if (descriptor.Category == WiredBoxCategory.Addon && WiredOctoberAddonBox.Names.Contains(descriptor.CanonicalName)) {
            box = new WiredOctoberAddonBox(_room, item, descriptor);
        }
        else if (descriptor.Category == WiredBoxCategory.Addon && WiredAddonModule.Names.Contains(descriptor.CanonicalName)) {
            box = new WiredAddonBox(_room, item, descriptor, _selectorState, _groups,
                context => WiredSelectorVariableBridge.Create(context, Variables.Module));
        }
        else if (descriptor.Category == WiredBoxCategory.Trigger && WiredTriggerConfiguration.Events.ContainsKey(descriptor.CanonicalName)) {
            box = WiredTriggerConfiguration.IsTimed(descriptor.CanonicalName)
                ? new WiredModernTimedTrigger(_room, item, descriptor, RoomTimerAfterLoad) : new WiredModernTrigger(_room, item, descriptor);
            defaults = WiredTriggerConfiguration.Defaults(descriptor.CanonicalName);
        }
        else if (descriptor.Category == WiredBoxCategory.Condition && WiredConditionConfiguration.Supports(descriptor.CanonicalName)) {
            // Calendar predicates use the configured zone; elapsed durations use the shared UTC room clock.
            box = new WiredModernCondition(_room, item, descriptor, _groups, ReadCounterMilliseconds,
                descriptor.CanonicalName is "wf_cnd_match_time" or "wf_cnd_match_date" ? () => ConditionCalendarTime
                    : descriptor.CanonicalName == "wf_cnd_date_rng_active" ? () => CalendarTime : () => _clock.GetUtcNow());
            defaults = WiredConditionConfiguration.Defaults(descriptor.CanonicalName);
        }
        else if (descriptor.Category == WiredBoxCategory.Action && WiredModernAction.Supports(descriptor.CanonicalName)) {
            var action = new WiredModernAction(_room, item, descriptor, _counters, @event => Dispatch(@event),
                DispatchWalkTransition, _roomLog, _logger, _clock, _rewards, _botStore, _clients, _definitions, _travelStore, transparentWalkTransition: true);
            _bindMovementPublication(action);
            box = action;
            defaults = WiredActionConfiguration.Defaults(descriptor.CanonicalName);
        }
        else if (WiredVariableExecutors.Supports(descriptor.CanonicalName) || WiredVariableMetadataBox.Supports(descriptor.CanonicalName)
            || WiredVariableAddonBox.Supports(descriptor.CanonicalName) || descriptor.CanonicalName is "wf_xtra_text_input_variable" or "wf_trg_var_changed"
            || descriptor.Category == WiredBoxCategory.Variable) {
            box = Variables.CreateBox(item);
        }

        if (box != null && box.Configuration.Origin == null && WiredNativeEditorProjection.DefaultRuntime(item.Id, descriptor) is { } fresh) {
            // Every mapped card opens on its compiled native defaults. They hold no picks, so no live room is needed yet.
            box.ApplyConfiguration(fresh);
        }
        else if (box != null && defaults != null) {
            if (!box.TryValidateConfiguration(defaults, out var validated, out var error)) {
                throw new InvalidDataException(error);
            }

            box.ApplyConfiguration(validated);
        }

        return box;
    }

    public void BeforeActorLeaves(RoomUser actor) => _engine.Mutate(() =>
    {
        _chests?.Leave(actor);
        WiredTemporaryEffects.For(_room).Forget(actor);
        _engine.ActorLeaving(actor);
        WiredAvatarState.For(_room).Thaw(actor);
        WiredAvatarState.For(_room).Forget(actor);
        WiredBotTargets.For(_room).Forget(actor);
        WiredGameState.For(_room).Forget(actor);

        return true;
    });

    public WiredRoomLogPage ReadLogs(int page, int amount, int level = -1, string query = "", int source = -1) =>
        _roomLog.Read(page, amount, level, query, source);

    public WiredMonitorSnapshot ReadMonitor() => new(_engine.ReadStats(), _engine.Limits.MaxExecutionsPerPass,
        _engine.Limits.MaxPendingStacks, _engine.Limits.MaxDepth, _roomLog.Summarize(MonitorHistory));

    public void ClearLogs() => _roomLog.Clear();

    /// <summary>
    /// Counts one occurrence on a monitor error row. Placement failure counts the placement-failure dialogs shown to the owner,
    /// not the failed runs: observed on AIR WIN63-202610131318-860371703, 9 dialogs over about 20 failed runs read as 9.
    /// The dialog's sender calls this with <see cref="WiredErrorKind.PlacementFailure"/> each time it shows one.
    /// </summary>
    public void RecordError(WiredErrorKind kind) => _errors.Record(kind);

    public IReadOnlyList<WiredErrorRow> ReadErrors() => _errors.Read();

    public void ClearErrors() => _errors.Clear();

    public WiredRoomStats ReadRoomStats()
    {
        int furni = 0, user = 0, global = 0;

        // Variables load with the first variable box, so a room that never built one has no permanent variables.
        if (_variables?.IsValueCreated == true) {
            var owner = _room.OwnerId > 0 ? (uint)_room.OwnerId : 0;

            foreach (var box in _variables.Value.Definitions) {
                if (!WiredVariableDefinitions.TryDecode(box.Descriptor.CanonicalName, box.Item.Id, _room.Id, owner, box.Configuration,
                        out var definition, out _) || definition is not { IsDurable: true, Link: null }) {
                    continue;
                }

                switch (definition.Target) {
                    case WiredVariableTarget.Furni:
                        furni++;
                        break;
                    case WiredVariableTarget.User:
                        user++;
                        break;
                    case WiredVariableTarget.Global:
                        global++;
                        break;
                }
            }
        }

        var items = _room.GetRoomItemHandler();

        return WiredRoomStats.Create(_engine.ReadExecutionCost(), _engine.Limits.ExecutionCostCap, items.GetFloor.Count,
            items.GetWall.Count, furni, user, global);
    }

    private void NoteLimit(WiredEngineLimit limit, string reason)
    {
        _errors.Record(limit switch
        {
            WiredEngineLimit.ExecutionBudget => WiredErrorKind.ExecutionCap,
            WiredEngineLimit.PendingStacks => WiredErrorKind.DelayedEventsCap,
            _ => WiredErrorKind.RecursionTimeout
        });
        _roomLog.Append(WiredRoomLog.ErrorLevel, limit switch
        {
            WiredEngineLimit.ExecutionBudget => WiredLogSource.ExecutionCap,
            WiredEngineLimit.PendingStacks => WiredLogSource.DelayedEventsCap,
            _ => WiredLogSource.RecursionTimeout
        }, 0, "", reason, _clock.GetUtcNow());
    }

    // Observed on AIR WIN63-202610131318-860371703: a failed wired furni placement shows every user in the room (owner and visitors
    // without rights alike) one NotificationDialog wired.error, also for timer runs, and only when at least 5.0 s have passed since that
    // user's last one. The rest of the stack still runs. The monitor's PLACEMENT_FAILURE count follows the runs that showed a dialog.
    private const long PlacementFailureCooldownMs = 5000;
    private readonly Dictionary<int, long> _lastPlacementFailureMs = [];

    internal void ReportPlacementFailure(Item box)
    {
        var now = _engine.NowMilliseconds;
        var shown = false;

        foreach (var user in _room.GetRoomUserManager().GetRoomUsers()) {
            if (user?.GetClient() is not { } client || client.GetHabbo() is not { } habbo
                || _lastPlacementFailureMs.TryGetValue(habbo.Id, out var last) && now - last < PlacementFailureCooldownMs) {
                continue;
            }

            _lastPlacementFailureMs[habbo.Id] = now;
            shown = true;
            client.Send(new Plus.Communication.Packets.Outgoing.Rooms.Notifications.RoomNotificationComposer("wired.error", new Dictionary<string, string>
            {
                ["image"] = "${image.library.url}album1584/WIRD2.png",
                ["message"] = "${room.error.wired.placement_failure}"
            }));
        }

        if (!shown) {
            return;
        }

        RecordError(WiredErrorKind.PlacementFailure);
        _roomLog.Append(WiredRoomLog.ErrorLevel, WiredLogSource.PlacementFailure, box.Id, box.Definition.ItemName,
            "room.error.wired.placement_failure", _clock.GetUtcNow());
    }

    // Observed on AIR WIN63-202610151745-318700621: a give from picked chests none of which is usable (518 reason 15) writes a DEBUG
    // line to the room log each time. The wired.error dialog TRANSACTION_FAILURE shows at most once per 20 s (none 19.0 s after the last
    // one, one 20.5 s after), and each dialog adds one TRANSACTION_FAILURE to the monitor and an ERROR line before that failure's DEBUG
    // line. A failure inside the window does not restart it. Observed on AIR WIN63-202610201032-107964540: the dialog goes to every user
    // in the room, whoever triggered the failure, and the 20 s window is shared by the whole room.
    private const long TransactionFailureCooldownMs = 20000;
    private long? _lastTransactionFailureMs;
    private readonly object _transactionFailureGate = new();

    internal void ReportTransactionFailure(RoomUser actor, uint sourceId)
    {
        if (actor.GetClient()?.GetHabbo() is not { } habbo) {
            return;
        }

        var now = _engine.NowMilliseconds;
        var at = _clock.GetUtcNow();
        bool show;

        lock (_transactionFailureGate) {
            show = _lastTransactionFailureMs is not { } last || now - last >= TransactionFailureCooldownMs;

            if (show) {
                _lastTransactionFailureMs = now;
            }
        }

        if (show) {
            _room.SendPacket(new Plus.Communication.Packets.Outgoing.Rooms.Notifications.RoomNotificationComposer("wired.error", new Dictionary<string, string>
            {
                ["image"] = "${image.library.url}album1584/WIRD2.png",
                ["message"] = "${room.error.wired.transaction_failure}"
            }));
            RecordError(WiredErrorKind.TransactionFailure);
            _roomLog.Append(WiredRoomLog.ErrorLevel, WiredLogSource.TransactionFailure, sourceId, "",
                "Wired error: TRANSACTION_FAILURE. (usage=1, users=1)", at);
        }

        _roomLog.Append(WiredRoomLog.DebugLevel, WiredLogSource.TransactionFailure, sourceId, "",
            $"Transaction failure: NO_WIRED_CHESTS_OR_LOCKED. (userId={habbo.Id}, userName={habbo.Username})", at);
    }

    public void AttachRoomItem(Item item) => _engine.Mutate(() =>
    {
        _highscores?.Invalidate();

        if (WiredChestFurniture.IsChest(item.Definition) && !item.IsTemporary) {
            Chests.Refresh(item);
        }

        if (Plus.HabboHotel.Items.Wired.Labels.WiredLabel.IsLabel(item) && !item.IsTemporary
            && ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)) {
            LabelAttached(item);
        }

        if (WiredCounterController.Recognizes(item) && ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)
            && (!_counterItems.TryGetValue(item.Id, out var previous) || !ReferenceEquals(previous, item))) {
            _counterItems[item.Id] = item;
            _counters.Attach(item);
        }

        if (item.IsTemporary && ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)
            && !_engine.TryGet(item.Id, out _)) {
            var box = CreateConfiguredBox(item);

            if (box != null) {
                AddBox(box);
            }
        }

        return true;
    });

    public void DetachRoomItem(Item item) => _engine.Mutate(() =>
    {
        _highscores?.Invalidate();
        ForgetFxItem(item);
        ForgetVisualization(unchecked((int)item.Id));

        if (Plus.HabboHotel.Items.Wired.Labels.WiredLabel.IsLabel(item)) {
            LabelDetached(item);
        }

        if (DescriptorOf(item)?.CanonicalName == "wf_xtra_var_leaderboard" && TryGet(item.Id, out _)) {
            LeaderboardRemoved(item.Id);
        }

        _counters.Forget(item);
        WiredProjectileFlights.For(_room).Forget(item);

        if (_counterItems.TryGetValue(item.Id, out var attached) && ReferenceEquals(attached, item)) {
            _counterItems.Remove(item.Id);
        }

        if (_variables?.IsValueCreated == true) {
            _variables.Value.ItemDetached(item);
        }

        TryRemove(item.Id);

        return true;
    });

    /// <summary>
    /// Boxes leaving the room for good (pickup, eject, room deletion) are placed again with default settings. Their saved
    /// settings are dropped first, so if that fails nothing changes and the boxes stay placed with their settings. Done in an
    /// engine pass: a save already on its way then finds the box detached and does not write the settings back.
    /// </summary>
    public void ResetRoomItems(IEnumerable<Item> items) => _engine.Mutate(() =>
    {
        var boxes = items.Where(item => item.IsWired && !item.IsTemporary).ToList();

        if (boxes.Count == 0) {
            return false;
        }

        ConfigurationStore.Reset(boxes.Select(item => item.Id).ToArray());

        foreach (var box in boxes) {
            DetachRoomItem(box);
        }

        return true;
    });

    public bool TryUseCounter(Item item, int parameter) => _engine.Mutate(() =>
    {
        if (!WiredCounterController.Recognizes(item) || !ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)) {
            return false;
        }

        AttachRoomItem(item);
        var mark = FurnitureStateEvents.Mark();
        var changed = _counters.Use(item, parameter, _engine.NowMilliseconds);
        PublishCounterChanges(_counters.TakeChanges(), mark);

        return changed;
    });

    private long? ReadCounterMilliseconds(Item item)
    {
        AttachRoomItem(item);

        return ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item) ? _counters.ReadMilliseconds(item) : null;
    }

    private void PollCounters(long now)
    {
        _chests?.Poll();

        foreach (var stale in _counterItems.Values.Where(item => !ReferenceEquals(_room.GetRoomItemHandler().GetItem(item.Id), item)).ToArray()) {
            DetachRoomItem(stale);
        }

        var mark = FurnitureStateEvents.Mark();
        PublishCounterChanges(_counters.Poll(now), mark);

        if (WiredBotTargets.For(_room).HasTargets) {
            foreach (var arrival in WiredBotTargets.For(_room).Poll(_room, now)) {
                QueueRuntimeEvent(arrival);
            }
        }

        PollProjectiles(now);
        FlushExternalChanges();
    }

    private void PollProjectiles(long now)
    {
        var flights = WiredProjectileFlights.For(_room);

        if (!flights.HasPending) {
            return;
        }

        foreach (var (item, key, before, after) in flights.Poll(now)) {
            var change = new WiredVariableChange(_room.Id, new(0, WiredVariableTarget.Furni, item.Id),
                before is null ? WiredVariableChangeKind.Created : after is null ? WiredVariableChangeKind.Removed : WiredVariableChangeKind.Updated,
                before is { } old ? new(old, null, null) : null, after is { } value ? new(value, null, null) : null, (int)item.Id, 0)
            { InternalKey = key };
            QueueRuntimeEvent(new(WiredEventKind.Variable)
            {
                Code = 0,
                Action = (int)change.Kind,
                VariableChange = change,
                PreviousValue = before ?? 0,
                Value = after ?? 0,
                EventItem = item
            }, 1);
        }
    }

    private void FlushExternalChanges()
    {
        // Display writes made by boxes are already queued; they are reported by the room's pass.
        var counters = _counters.TakeChanges();

        if (counters.Count > 0) {
            _engine.SealExternalPublication();
        }

        PublishCounterChanges(counters, FurnitureStateEvents.Mark());

        if (_variables?.IsValueCreated != true) {
            return;
        }

        var variables = _variables.Value.DrainChanges();

        if (variables.Count > 0) {
            _engine.SealExternalPublication();
            LeaderboardValuesChanged(variables);
        }

        foreach (var change in variables) {
            QueueRuntimeEvent(new(WiredEventKind.Variable)
            {
                Code = unchecked((int)change.Key.DefinitionId),
                Action = (int)change.Kind,
                VariableChange = change,
                PreviousValue = change.Before?.Value ?? 0,
                Value = change.After?.Value ?? 0,
                EventItem = change.Key.Target == WiredVariableTarget.Furni ? _room.GetRoomItemHandler().GetItem((uint)change.EntityId) : null,
                TargetUser = change.Key.Target == WiredVariableTarget.User ? _room.GetRoomUserManager().GetRoomUserByVirtualId(change.EntityId) : null
            }, change.Depth + 1);
        }
    }

    private void QueueRuntimeEvent(WiredRuntimeEvent @event, int? depth = null)
    {
        if (!_engine.Enqueue(@event, depth)) {
            _logger.LogWarning("Wired {EventKind} event rejected by room queue/depth limits in room {RoomId}", @event.Kind, _room.Id);
        }
    }

    // A display change is reported only for a display write this thread made after the mark.
    private void PublishCounterChanges(IEnumerable<WiredCounterChange> changes, long mark)
    {
        foreach (var change in changes) {
            if (!ReferenceEquals(_room.GetRoomItemHandler().GetItem(change.Item.Id), change.Item)) {
                continue;
            }

            if (change.Event.Kind == WiredEventKind.GameStart) {
                _room.GetGameManager().Reset();
                WiredGameState.For(_room).ResetQuotas();
            }

            if (change.DisplayChanged) {
                // Data-only publications keep the full state bookkeeping once and send their own frame below.
                change.Item.UpdateState(true, change.Publication == WiredCounterPublication.Object);
            }

            if (change.Publication != WiredCounterPublication.Object && (change.DisplayChanged || change.ForceDisplay)) {
                if (change.Publication != WiredCounterPublication.BatchData || !TryQueueBatchData(change.Item)) {
                    _room.SendObjectData(change.Item, change.Text, change.Publication == WiredCounterPublication.BatchData);
                }
            }

            if (change.Event.Kind == WiredEventKind.StateChanged
                && (!change.DisplayChanged || !FurnitureStateEvents.TakeWriteSince(change.Item, mark))) {
                continue;
            }

            QueueRuntimeEvent(change.Event);
        }
    }
}

public sealed record WiredMonitorSnapshot(WiredEngineWindow Engine, int ExecutionsPerPass, int PendingLimit, int DepthLimit, WiredRoomLogSummary Logs);
