using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Triggers;

public class WiredModernTrigger : WiredModernBox, IWiredClickTrigger
{
    public WiredModernTrigger(Room room, Item item, WiredBoxDescriptor descriptor) : base(room, item, descriptor)
    {
        if (!WiredTriggerConfiguration.Events.TryGetValue(descriptor.CanonicalName, out var kind)) {
            throw new ArgumentException("Unknown trigger.", nameof(descriptor));
        }

        Events = [kind];
    }
    private (int Threshold, int Team)? _scoreSettings;
    public IReadOnlyCollection<WiredEventKind> Events { get; }
    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)) {
            if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)) {
                validated = proposed;
                error = "Invalid native trigger authority.";

                return false;
            }

            return WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out validated, out error);
        }

        if (!WiredTriggerConfiguration.TryValidate(Descriptor.CanonicalName, proposed, out validated, out error)) {
            return false;
        }

        if (Descriptor.CanonicalName == "wf_trg_recv_signal"
            && validated.FurniSources.GetValueOrDefault("items") == WiredSources.Selected
            && Instance.GetRoomItemHandler() is { } handler
            && validated.SelectedItems.Select(handler.GetItem).OfType<Item>().Any(item => !WiredStackEngine.IsSignalAntenna(item))) {
            error = "wiredfurni.error.require_antenna_furni";

            return false;
        }

        return true;
    }
    public bool HidesChat(WiredRuntimeContext context) => Descriptor.CanonicalName == "wf_trg_says_something"
        && WiredTriggerPredicates.HidesChat(context.ConfigurationOf(this));

    public override bool Execute(WiredRuntimeContext context) => Matches(context, afterSelectors: false);

    // Saving the same score settings keeps a spent box spent.
    protected override void ConfigurationChanged()
    {
        if (Descriptor.CanonicalName != "wf_trg_score_achieved" || !TryValidateConfiguration(Configuration, out var config, out _)) {
            return;
        }

        var settings = (Param(config, 0, 1), Param(config, 1));

        if (_scoreSettings is { } previous && previous != settings) {
            WiredGameState.For(Instance).RearmScoreTrigger(Item.Id);
        }

        _scoreSettings = settings;
    }

    public bool CanTrigger(WiredRuntimeContext context)
    {
        if (!TryValidateConfiguration(context.ConfigurationOf(this), out var config, out _)
            || (config.FurniSources.Values.Contains(WiredSources.Selector) || config.UserSources.Values.Contains(WiredSources.Selector))
                && !Matches(context, afterSelectors: true)) {
            return false;
        }

        return true;
    }

    /// <summary>After the conditions, a score trigger adds the scoring team's current members to the selection.</summary>
    internal void AfterConditions(WiredRuntimeContext context)
    {
        if (Descriptor.CanonicalName == "wf_trg_score_achieved") {
            context.Selected.UserIds.UnionWith(context.Targets.ResolveUsers(context, [], WiredSources.AllRoom, raw: true)
                .Where(user => !user.IsBot && (int)user.Team == context.Event.Team).Select(user => user.VirtualId));
        }
    }

    /// <summary>A Receive Signal that picks its antennas cannot take a signal addressed to another one.</summary>
    internal bool CannotReceiveSignal(uint antenna) => Descriptor.CanonicalName == "wf_trg_recv_signal"
        && Configuration.FurniSources.TryGetValue("items", out var source) && source == WiredSources.Selected
        && !Configuration.SelectedItems.Contains(antenna);

    private bool Matches(WiredRuntimeContext context, bool afterSelectors)
    {
        if (!Events.Contains(context.Event.Kind)) {
            return false;
        }

        var config = context.ConfigurationOf(this);

        if (!TryValidateConfiguration(config, out config, out _)) {
            return false;
        }

        var evt = context.Event;
        var name = Descriptor.CanonicalName;
        // The selector pool is empty until the stack's selectors run, so those checks wait for CanTrigger.
        var deferItems = !afterSelectors && config.FurniSources.GetValueOrDefault("items") == WiredSources.Selector;
        var deferBots = !afterSelectors && config.UserSources.GetValueOrDefault("bots") == WiredSources.Selector;
        Item[] Items() => Furni(context, config, "items");
        bool ItemMatches(bool state = false) => evt.EventItem is { } item
            && (deferItems || WiredTriggerPredicates.MatchesItem(config, item, Items(), state));
        bool BotMatches() => evt.Actor?.IsBot == true && !evt.Actor.IsPet
            && WiredTriggerPredicates.MatchesName(config, WiredModernCondition.Name(evt.Actor))
            && (deferBots || (config.UserSources["bots"] == 0
                ? context.Targets.AllUsers().Any(user => ReferenceEquals(user, evt.Actor))
                : Users(context, config, "bots", config.Text).Contains(evt.Actor)));

        return name switch
        {
            "wf_trg_enter_room" or "wf_trg_leave_room" => evt.Actor != null && WiredTriggerPredicates.MatchesName(config, WiredModernCondition.Name(evt.Actor)),
            "wf_trg_says_something" => evt.Actor != null && WiredTriggerPredicates.MatchesChat(config, evt.Message, evt.Actor.HabboId == context.Room.OwnerId),
            "wf_trg_walks_on_furni" or "wf_trg_walks_off_furni" or "wf_trg_click_furni" => evt.Actor != null && ItemMatches(),
            "wf_trg_stuff_state" or "wf_trg_state_changed" => ItemMatches(true),
            "wf_trg_click_tile" => evt.Actor != null && (deferItems
                || Items().Any(item => WiredRoomOperations.Footprint(item, item.GetX, item.GetY, item.Rotation).Contains(new(evt.X, evt.Y)))),
            "wf_trg_click_user" => evt.Actor != null && evt.TargetUser != null,
            "wf_trg_bot_reached_avtr" => evt.TargetUser != null && BotMatches(),
            // With user source 100 the name must match exactly, ignoring case, so an empty name matches no bot. With user source 200 only the user selection decides.
            "wf_trg_bot_reached_stf" => evt.Actor is { IsBot: true, IsPet: false } && ItemMatches()
                && (config.UserSources["bots"] == WiredSources.Selector
                    ? deferBots || Users(context, config, "bots").Contains(evt.Actor)
                    : config.Text.Length != 0 && Users(context, config, "bots", config.Text).Contains(evt.Actor)),
            "wf_trg_clock_counter" => ItemMatches() && WiredTriggerPredicates.MatchesCounter(config, evt.PreviousValue, evt.Value),
            "wf_trg_score_achieved" => WiredTriggerPredicates.MatchesScore(config, evt.Team, (int)evt.PreviousValue, (int)evt.Value)
                && WiredGameState.For(Instance).TryFireScoreTrigger(Item.Id),
            "wf_trg_user_performs_action" => evt.Actor != null && WiredTriggerPredicates.MatchesAction(config, evt.Action, evt.Code),
            "wf_trg_recv_signal" => context.Signal != null && (deferItems || Items().Any(item => item.Id == unchecked((uint)evt.Code))),
            "wf_trg_game_starts" or "wf_trg_game_ends" => true,
            "wf_trg_room_initializes" => true,
            "wf_trg_receive_message" or "wf_trg_receive_dataframe" => evt.Link != null && evt.EventItem is { } endpoint
                && (!afterSelectors && config.FurniSources.GetValueOrDefault("endpoints") == WiredSources.Selector
                    || Furni(context, config, "endpoints").Any(item => item.Id == endpoint.Id)) && (name == "wf_trg_receive_message" || CaptureDataframe(context, config)),
            "wf_trg_collision" => evt.Actor != null && evt.EventItem != null,
            "wf_trg_at_given_time" or "wf_trg_at_time_long" or "wf_trg_periodically" or "wf_trg_period_short" or "wf_trg_period_long" =>
                ReferenceEquals(evt.EventItem, Item),
            _ => throw new InvalidOperationException("Trigger has no event predicate.")
        };
    }

    /// <summary>
    /// LinkDataframeReceivePreset rows: kind 1 writes the row's number into the picked context variable. Placeholder rows (kind 0) are
    /// read by WiredTextPlaceholders. A row whose key the dataframe lacks, or whose value is not a number, is left alone.
    /// </summary>
    private static bool CaptureDataframe(WiredRuntimeContext context, WiredConfiguration config)
    {
        if (WiredLinkDataframeCodec.Decode(config.Text, Param(config, 0)) is not { } rows) {
            return false;
        }

        var values = new Dictionary<uint, long>();

        for (var row = 0; row < rows.Length; row++) {
            if (Param(config, 1 + row) != WiredLinkDataframeCodec.VariableDestination || config.VariableIds.Length <= row
                || !WiredVariableDescription.TryParseCatalogId(config.VariableIds[row], out var target, out var token)
                || target != WiredVariableTarget.Context || !WiredVariableModule.TryDefinitionId(token, out var definition)
                || context.Event.Link!.Rows.FirstOrDefault(sent => sent.Key == WiredLinkDataframeCodec.NormalizeKey(rows[row].Key)) is not { } value
                || (value.Number ?? (long.TryParse(value.Text, System.Globalization.NumberStyles.AllowLeadingSign,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : null)) is not { } number) {
                continue;
            }

            values[definition] = number;
        }

        if (values.Count == 0) {
            return true;
        }

        context.VariableFrame = WiredVariableRuntimeFrames.Create(context, context.VariableFrame);

        return context.Room.GetWired().Variables.Module.CaptureContextValues(values, context.VariableFrame);
    }

    public (bool BlockMenu, bool DoNotRotate) ClickSettings(WiredRuntimeContext context) =>
        Descriptor.CanonicalName == "wf_trg_click_user" ? (Param(context.ConfigurationOf(this), 0) == 1, Param(context.ConfigurationOf(this), 1) == 1) : (false, false);
}

public sealed class WiredModernTimedTrigger : WiredModernTrigger, IWiredTimedTrigger
{
    private readonly WiredTimedTriggers _timers = new();
    private long _started;
    private long _epoch;
    // At Given Time with 0 pulses starts only from a room timer reset, not from being added to the room.
    private bool _resetByAction;
    private readonly Func<long?>? _roomTimer;
    private const long RoomTimerMarginMs = 20;
    public WiredModernTimedTrigger(Room room, Item item, WiredBoxDescriptor descriptor, Func<long?>? roomTimer = null) : base(room, item, descriptor)
    {
        if (!WiredTriggerConfiguration.IsTimed(descriptor.CanonicalName)) {
            throw new ArgumentException("Not a timed trigger.", nameof(descriptor));
        }

        _roomTimer = roomTimer;
    }
    public WiredRuntimeEvent? Poll(long nowMilliseconds)
    {
        if (!TryValidateConfiguration(Configuration, out var config, out _)) {
            return null;
        }

        // A box with 0 pulses does not fire when saved, and fires on the first room tick after each timer reset.
        if (Descriptor.CanonicalName == "wf_trg_at_given_time" && config.IntParams is [0]
            && (!_resetByAction || nowMilliseconds < WiredTimedTriggers.FirstRoomTickAfter(_started))) {
            return null;
        }

        return _timers.TryFire(Descriptor.CanonicalName, Item.Id, config, nowMilliseconds,
            Math.Max(0, nowMilliseconds - WiredTimedTriggers.FirstRoomTickAfter(_started)), _epoch)
            ? new(Events.Single()) { EventItem = Item } : null;
    }
    public void Reset(long nowMilliseconds)
    {
        var roomTimer = _roomTimer?.Invoke();

        // A placed, saved or moved At Given Time box counts from the last timer reset. The poll counts from the first room tick after the start, so the
        // start is put a little before the room timer began: the engine clock and the room timer are two clocks cut to whole ms.
        var counted = Descriptor.CanonicalName == "wf_trg_at_given_time" && roomTimer is { } elapsed ? nowMilliseconds - elapsed - RoomTimerMarginMs : nowMilliseconds;
        ResetElapsed(counted);
        _resetByAction = false;
        _timers.Forget(Item.Id);

        if (roomTimer is { } timer && TryValidateConfiguration(Configuration, out var config, out _)) {
            _timers.Join(Descriptor.CanonicalName, Item.Id, config, timer, _epoch);
        }
    }
    public void ResetElapsed(long nowMilliseconds)
    {
        _resetByAction = true;
        _started = nowMilliseconds;
        _epoch++;
        _timers.ResetPeriodic(Item.Id, nowMilliseconds);
    }
}
