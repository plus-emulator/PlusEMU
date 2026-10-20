using System.Globalization;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

public enum WiredClockOrigin
{
    Unknown, ModernWired, Gui
}
public enum WiredClockReason
{
    Start, Stop, Reset, Pause, Resume, Adjust, Expiry, GameEnd, Detach
}
public sealed record WiredClockTransition(Item Item, object Clock, long Sequence, long Placement, ItemDefinition Definition,
    WiredClockOrigin Origin, WiredClockReason Reason, bool WasRunning, bool WasStarted, RoomUser? Actor, WiredClockReason? PreviousReason)
{
    internal bool OtherFootballClockRunning { get; init; }
    internal WiredClockOrigin? PreviousOrigin { get; init; }
    internal int X { get; } = Item.GetX;
    internal int Y { get; } = Item.GetY;
    internal double Z { get; } = Item.GetZ;
    internal int Rotation { get; } = Item.Rotation;
    internal uint ItemId { get; } = Item.Id;
    internal uint RoomId { get; } = Item.RoomId;
    internal uint OwnerId { get; } = Item.OwnerId;
    internal uint BaseItem { get; } = Definition.Id;
    internal string DefinitionName { get; } = Definition.ItemName;
}

/// <summary>How a display change reaches clients: the full object update, or the legacy data only (one object / a batch).</summary>
public enum WiredCounterPublication
{
    Object, SingleData, BatchData
}

/// <summary><paramref name="Text"/> is the display this change wrote; a later drain must send it, not the item's final string.</summary>
public sealed record WiredCounterChange(Item Item, WiredRuntimeEvent Event, bool DisplayChanged,
    WiredCounterPublication Publication = WiredCounterPublication.Object, bool ForceDisplay = false, string Text = "");

/// <summary>Room-owned clock state. The room drains changes and publishes packets/events once.</summary>
/// <remarks>Observed on AIR WIN63-202610151745-318700621: a game counter started at 99:59.5 goes on to 6000, 6001 and 6002 seconds.
/// No upper limit is visible, so the default limit only keeps the saved seconds inside an int.</remarks>
public sealed class WiredCounterController(long maxHalfSeconds = int.MaxValue * 2L, Action<Item, bool>? gameControl = null, int defaultGameSeconds = 30,
    Action<WiredClockTransition>? observeTransition = null, Action<WiredClockTransition>? gameTransition = null,
    Func<Item, WiredClockOrigin, RoomUser?, bool>? canBegin = null)
{
    private sealed class Clock(Item item, bool game, bool countdown, long value, long duration)
    {
        public Item Item { get; } = item;
        public long Duration { get; } = duration; // Half seconds a native game timer restarts and resets to.
        public bool GameAware { get; } = game;
        public bool Countdown { get; } = countdown;
        public long Value = value; // Half seconds: elapsed for Wired counters, remaining for native game timers.
        public bool Running;
        public bool Started = !countdown && value > 0;
        public long NextTick;
        public long Sequence;
        public WiredClockReason? LastReason;
        public WiredClockOrigin? LastOrigin;
        public bool AdjustedSinceStop;
    }
    private readonly Dictionary<uint, Clock> _clocks = [];
    private readonly Queue<WiredCounterChange> _changes = [];
    internal bool HasPendingChanges => _changes.Count > 0;
    public bool HasRunning => _clocks.Values.Any(clock => clock.Running);
    /// <summary>A Wired game counter (wf_game_upcounter*) is counting: the room has a running game.</summary>
    public bool HasRunningGameCounter => _clocks.Values.Any(clock => clock.Running && clock.GameAware && !clock.Countdown);
    public static bool Recognizes(Item item) => IsWiredCounter(item.Definition) || IsGameTimer(item);
    public static bool IsWiredCounter(ItemDefinition definition) => Name(definition) is "wf_upcounter1" or "wf_upcounter2" or "wf_game_upcounter1"
        or "wf_game_upcounter2";
    public static bool IsGameTimer(Item item) => item.Definition.ItemName is "bb_counter" or "fball_counter" or "es_counter"
        || Name(item) == "game_timer" || item.Definition.InteractionType is InteractionType.Banzaicounter or InteractionType.Freezetimer
        || item.Definition.InteractionType == InteractionType.Counter && !Name(item).StartsWith("wf_", StringComparison.Ordinal);
    /// <summary>The state a native game timer saves: its saved duration in seconds. Null for every other item.</summary>
    public static string? StoredState(Item item) => null;
    /// <summary>A newly placed native game timer starts again at 30 seconds.</summary>
    public static void PlacedNew(Item item)
    {
    }
    private static string Name(Item item) => Name(item.Definition);
    private static string Name(ItemDefinition definition) => string.IsNullOrEmpty(definition.InteractionName) ? definition.ItemName : definition.InteractionName;
    public bool Attach(Item item)
    {
        if (!Recognizes(item)) {
            return false;
        }

        var countdown = IsGameTimer(item);
        var game = countdown || Name(item).StartsWith("wf_game_", StringComparison.Ordinal);
        var seconds = int.TryParse(item.LegacyDataString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var saved) ? Math.Max(0, saved) : 0;
        var value = countdown ? seconds * 2L : Math.Min(maxHalfSeconds, seconds * 2L);
        _clocks[item.Id] = new(item, game, countdown, value, countdown && value > 0 ? value : defaultGameSeconds * 2L);

        return true;
    }
    public long? ReadMilliseconds(Item item) => Find(item) is { } clock ? clock.Value * 500L : null;
    public bool IsRunning(Item item) => Find(item)?.Running == true;
    public int? ReadState(Item item) => Find(item) is { } clock ? clock.Running ? 1 : clock.Started ? 2 : 0 : null;
    public long? ReadPulseCount(Item item) => Find(item)?.Value;
    public bool? ReadGameAware(Item item) => Find(item)?.GameAware;
    public bool SetPulseCount(Item item, int value)
    {
        var clock = Find(item);

        if (clock == null) {
            return false;
        }

        Evidence(clock, WiredClockReason.Adjust);
        var previous = clock.Value;
        clock.Value = Math.Clamp(clock.Countdown ? value / 2 * 2L : value, 0,
            clock.Countdown ? int.MaxValue * 2L : maxHalfSeconds);
        PublishClock(clock, previous);

        return true;
    }
    public void Forget(Item item)
    {
        if (Find(item) is { } clock) {
            Evidence(clock, WiredClockReason.Detach);
            _clocks.Remove(item.Id);
        }
    }
    public void Clear()
    {
        _clocks.Clear();
        _changes.Clear();
    }
    private Clock? Find(Item item) => _clocks.TryGetValue(item.Id, out var clock) && ReferenceEquals(clock.Item, item) ? clock : null;

    // Raw Volt clock control: 0 start, 1 stop, 2 reset, 3 pause, 4 resume.
    public bool Control(Item item, int operation, long nowMilliseconds, WiredClockOrigin origin = WiredClockOrigin.Unknown, RoomUser? actor = null)
    {
        var clock = Find(item);

        if (clock == null || operation is < 0 or > 4) {
            return false;
        }

        var restartGame = operation == 0 && clock.Running && clock.GameAware && !clock.Countdown
            && origin == WiredClockOrigin.ModernWired;

        if (operation == 0 && (!clock.Running || restartGame) && canBegin?.Invoke(item, origin, actor) == false) {
            return false;
        }

        // Observed on AIR WIN63-202610151745-318700621: Start restarts a running Wired game counter and fires both game triggers.
        if (restartGame) {
            Stop(clock, WiredClockReason.Stop, origin, actor);
        }

        if (operation == 2) {
            Stop(clock, WiredClockReason.Reset, origin, actor);

            if (!clock.Running) {
                Evidence(clock, WiredClockReason.Reset, origin, actor);
            }

            clock.Started = false;
            var previous = clock.Value;
            clock.Value = clock.Countdown ? clock.Duration : 0;
            PublishClock(clock, previous, ModernBatch(origin));
            ReportState(clock);
        }

        // Observed on AIR WIN63-202610131318-860371703: a Start keeps a value adjusted after the last stop, and restarts from 0 otherwise.
        // Observed on AIR WIN63-202610131318-860371703 (wf_game_upcounter1, 8/8): game counters reset the same way, the "0" in the Start's batch.
        if (operation == 0 && origin == WiredClockOrigin.ModernWired && !clock.Running && !clock.Countdown
            && clock.Value != 0 && !clock.AdjustedSinceStop) {
            var previous = clock.Value;
            clock.Value = 0;
            PublishClock(clock, previous, WiredCounterPublication.BatchData);
        }

        if (operation is 0 or 4 && !clock.Running) {
            if (clock.Countdown && clock.Value <= 0) {
                var previous = clock.Value;
                clock.Value = clock.Duration;
                PublishClock(clock, previous);
            }

            var transition = Evidence(clock, operation == 0 ? WiredClockReason.Start : WiredClockReason.Resume, origin, actor);
            clock.Running = true;
            clock.Started = true;
            clock.AdjustedSinceStop = false;
            // Observed on AIR WIN63-202610131318-860371703: a started counter shows its first "1" on the 2nd 500 ms room boundary strictly after
            // the firing (501-1000 ms later, exactly 1000 ms for an on-boundary firing), then every 1000 ms on that grid.
            clock.NextTick = clock.Countdown ? nowMilliseconds + 1000 : NextBoundary(nowMilliseconds);
            ReportState(clock);

            if (clock.Countdown) {
                if (gameTransition != null) {
                    gameTransition(transition);
                }
                else {
                    gameControl?.Invoke(item, true);
                }
            }
            else if (clock.GameAware && operation == 0) {
                // Observed on AIR WIN63-202610131318-860371703: starting a game counter starts the game in the same tick.
                gameTransition?.Invoke(transition);
            }
        }

        if (operation is 1 or 3 && clock.Running) {
            Stop(clock, operation == 1 ? WiredClockReason.Stop : WiredClockReason.Pause, origin, actor);
            ReportState(clock);
        }

        return true;
    }
    // Volt operators: 0 increase, 1 decrease, 2 set.
    public bool Adjust(Item item, int operation, int minutes, int halfSecondSteps, WiredClockOrigin origin = WiredClockOrigin.Unknown)
    {
        var clock = Find(item);

        if (clock == null || operation is < 0 or > 2 || minutes is < 0 or > 99 || halfSecondSteps is < 0 or > 119) {
            return false;
        }

        var amount = minutes * 120L + halfSecondSteps;

        if (clock.Countdown) {
            amount = amount / 2 * 2;
        }

        Evidence(clock, WiredClockReason.Adjust, origin);
        clock.AdjustedSinceStop = !clock.Running;
        var previous = clock.Value;
        clock.Value = Math.Clamp(operation switch { 0 => previous + amount, 1 => previous - amount, _ => amount }, 0,
            clock.Countdown ? int.MaxValue * 2L : maxHalfSeconds);
        // Observed on AIR WIN63-202610151745-318700621: a Wired increase, decrease or set sends the value as data in an ObjectsDataUpdate.
        PublishClock(clock, previous, ModernBatch(origin), operation == 2 && origin == WiredClockOrigin.ModernWired);

        return true;
    }
    public bool Use(Item item, int parameter, long nowMilliseconds)
    {
        var clock = Find(item);

        if (clock == null) {
            return false;
        }

        return Control(item, parameter == 2 ? 2 : clock.Running ? 1 : 0, nowMilliseconds, WiredClockOrigin.Gui);
    }
    public void OnGameEnded()
    {
        foreach (var clock in _clocks.Values) {
            if (clock.GameAware && clock.Running) {
                Evidence(clock, WiredClockReason.GameEnd);
                clock.Running = false;
            }
        }
    }
    public IReadOnlyList<WiredCounterChange> Poll(long nowMilliseconds)
    {
        foreach (var clock in _clocks.Values) {
            if (!clock.Running || nowMilliseconds < clock.NextTick) {
                continue;
            }

            if (clock.Countdown ? clock.Value <= 0 : clock.Value >= maxHalfSeconds) {
                Stop(clock);
                continue;
            }

            var previous = clock.Value;
            clock.Value = clock.Countdown ? Math.Max(0, clock.Value - 2) : clock.Value + 1;
            clock.NextTick = clock.Countdown ? nowMilliseconds + 1000 : NextBoundary(nowMilliseconds);
            PublishClock(clock, previous, clock.LastOrigin == WiredClockOrigin.ModernWired ? WiredCounterPublication.SingleData : WiredCounterPublication.Object);

            if (clock.Countdown && clock.Value == 0) {
                Stop(clock);
            }
        }

        return TakeChanges();
    }
    // The first 500 ms room boundary strictly after the instant: the half-second steps of a counter sit on the room's state update grid.
    private static long NextBoundary(long nowMilliseconds) =>
        (nowMilliseconds / WiredFlashStates.StateUpdateMilliseconds + 1) * WiredFlashStates.StateUpdateMilliseconds;
    public IReadOnlyList<WiredCounterChange> TakeChanges()
    {
        var result = new List<WiredCounterChange>();

        while (_changes.TryDequeue(out var change)) {
            if (Find(change.Item) != null) {
                result.Add(change);
            }
        }

        return result;
    }
    private void Stop(Clock clock, WiredClockReason reason = WiredClockReason.Expiry, WiredClockOrigin origin = WiredClockOrigin.Unknown, RoomUser? actor = null)
    {
        if (!clock.Running) {
            return;
        }

        var transition = Capture(clock, reason, origin, actor);
        clock.Running = false;
        observeTransition?.Invoke(transition);

        if (clock.Countdown) {
            if (gameTransition != null) {
                gameTransition(transition);
            }
            else {
                gameControl?.Invoke(clock.Item, false);
            }
        }
        else if (clock.GameAware && reason != WiredClockReason.Pause) {
            // Observed on AIR WIN63-202610131318-860371703: stopping a game counter ends the game in the same tick.
            gameTransition?.Invoke(transition);
        }
    }
    private WiredClockTransition Evidence(Clock clock, WiredClockReason reason, WiredClockOrigin origin = WiredClockOrigin.Unknown, RoomUser? actor = null)
    {
        var transition = Capture(clock, reason, origin, actor);
        observeTransition?.Invoke(transition);

        return transition;
    }
    private WiredClockTransition Capture(Clock clock, WiredClockReason reason, WiredClockOrigin origin, RoomUser? actor)
    {
        var transition = new WiredClockTransition(clock.Item, clock, ++clock.Sequence, clock.Item.Placement, clock.Item.Definition,
            origin, reason, clock.Running, clock.Started, actor, clock.LastReason)
        {
            PreviousOrigin = clock.LastOrigin,
            OtherFootballClockRunning = _clocks.Values.Any(other => !ReferenceEquals(other, clock) && other.Running
                && other.Item.Definition.ItemName == "fball_counter")
        };
        clock.LastReason = reason;
        clock.LastOrigin = origin;

        return transition;
    }
    // Observed on AIR WIN63-202610131318-860371703: Start, Stop and Reset change a Wired counter's furni state once each,
    // also when the displayed value stays the same.
    private static void ReportState(Clock clock)
    {
        if (!clock.Countdown && clock.Item.GetRoom() is { } room) {
            FurnitureStateEvents.Record(room, clock.Item);
        }
    }
    // Only Wired-driven counters publish the data-only frames; game timers and GUI/unknown writers keep the object update.
    private static WiredCounterPublication ModernBatch(WiredClockOrigin origin) =>
        origin == WiredClockOrigin.ModernWired ? WiredCounterPublication.BatchData : WiredCounterPublication.Object;
    private void PublishClock(Clock clock, long previous, WiredCounterPublication publication = WiredCounterPublication.Object, bool force = false) => PublishDisplay(clock,
        new(WiredEventKind.Counter) { EventItem = clock.Item, PreviousValue = previous * 500L, Value = clock.Value * 500L }, publication, force);
    private void PublishDisplay(Clock clock, WiredRuntimeEvent @event, WiredCounterPublication publication, bool force)
    {
        var text = (clock.Value / 2).ToString(CultureInfo.InvariantCulture);
        var changed = clock.Item.LegacyDataString != text;

        if (changed) {
            // Observed on AIR WIN63-202610131318-860371703: a Wired counter's ticks and adjusts do not change its furni state.
            using (!clock.Countdown && clock.Item.GetRoom() is { } room ? FurnitureStateEvents.Unreported(room, clock.Item) : null) {
                clock.Item.LegacyDataString = text;
            }
        }

        // Observed on AIR WIN63-202610131318-860371703: a Wired-started game counter sends each second as ObjectDataUpdate like a plain counter;
        // only the native game timers keep the object update.
        if (clock.Countdown) {
            publication = WiredCounterPublication.Object;
            force = false;
        }

        _changes.Enqueue(new(clock.Item, @event, changed, publication, force, text));
    }
}
