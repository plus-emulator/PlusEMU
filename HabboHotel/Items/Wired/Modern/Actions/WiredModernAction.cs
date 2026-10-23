using Plus.HabboHotel.Permissions;
using System.Collections.Immutable;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

public sealed class WiredModernAction : WiredModernBox, IWiredContextualAction
{
    /// <summary>The whisper a Kick User sends the room owner it cannot kick (hhus text).</summary>
    internal const string KickOwnerException = "Wired kick exception: Room owner";
    private readonly WiredCounterController _clocks;
    private readonly Action<WiredRuntimeEvent> _publish;
    private Action<WiredRuntimeEvent>? _headingPublisher;
    private Func<WiredRuntimeContext, WiredRuntimeEvent, bool?>? _headingCollision;
    private readonly WiredRoomMovement _movement;
    private readonly WiredRoomLog _roomLog;
    private readonly WiredDirectionalActions _directions = new();
    private readonly ILogger _logger;
    private readonly TimeProvider _clock;
    private readonly Random? _random;
    private readonly IWiredRewardService _rewards;
    private readonly IBotManagementStore _botStore;
    private readonly IGameClientManager _clients;
    private readonly IItemDataManager _definitions;
    private readonly IItemTravelStore _travelStore;
    public static readonly IReadOnlySet<string> OtherNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_act_control_clock", "wf_act_adjust_clock", "wf_act_reset_timers", "wf_act_call_stacks", "wf_act_neg_call_stacks",
        "wf_act_send_signal", "wf_act_neg_send_signal", "wf_act_log", "wf_act_neg_log", "wf_act_show_message", "wf_act_click_conf",
        "wf_act_chase", "wf_act_flee", "wf_act_move_to_dir", "wf_act_move_rotate_user", "wf_act_freeze", "wf_act_unfreeze",
        "wf_act_join_team", "wf_act_leave_team", "wf_act_give_score", "wf_act_give_score_tm", "wf_act_kick_user", "wf_act_mute_triggerer", "wf_act_teleport_to_room", "wf_act_give_reward"
    };
    public static bool Supports(string name) => WiredTemporaryFurnitureActions.Supports(name) || WiredMovementActions.Names.Contains(name) || OtherNames.Contains(name) || WiredBotActions.Names.Contains(name)
        || WiredOctoberActions.Names.Contains(name);
    public bool IsNegative => Descriptor.CanonicalName is "wf_act_neg_call_stacks" or "wf_act_neg_send_signal" or "wf_act_neg_log";

    public WiredModernAction(Room room, Item item, WiredBoxDescriptor descriptor, WiredCounterController clocks,
        Action<WiredRuntimeEvent> publish, Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walkTransition, WiredRoomLog roomLog, ILogger logger,
        TimeProvider clock, IWiredRewardService rewards, IBotManagementStore botStore, IGameClientManager clients, IItemDataManager definitions,
        IItemTravelStore travelStore, bool transparentWalkTransition = false, Random? movementRandom = null) : base(room, item, descriptor)
    {
        if (!Supports(descriptor.CanonicalName)) {
            throw new ArgumentException("Unknown action.", nameof(descriptor));
        }

        _clocks = clocks;
        _publish = publish;
        _movement = new(walkTransition, transparentWalkTransition);
        _roomLog = roomLog;
        _logger = logger;
        _clock = clock;
        _rewards = rewards;
        _botStore = botStore;
        _clients = clients;
        _definitions = definitions;
        _travelStore = travelStore;
        _random = movementRandom;
    }

    // Saving move_to_dir restarts every furni from the configured direction; the heading is kept only across firings.
    // Observed on hhus (AIR WIN63-202610131318-860371703): re-saves with direction 3, then 1, moved the same tile (+1,+1), then (+1,-1).
    protected override void ConfigurationChanged() => _directions.Clear();

    internal void BindHeadingCollision(Func<WiredRuntimeContext, WiredRuntimeEvent, bool?> enqueue)
    {
        _headingPublisher = _publish;
        _headingCollision = enqueue;
    }

    internal bool WalkBindingIsCurrent(Action<RoomUser, IEnumerable<Item>, IEnumerable<Item>> walk) =>
        _movement.WalkBindingIsCurrent(walk);

    internal void BindCarryPublication(Action<WiredRuntimeContext, IReadOnlyList<RoomUser>> prepare,
        Func<WiredRuntimeContext, RoomUser, WiredMovementComposer, WiredMoveStyleComposer, bool> append) =>
        _movement.BindCarryPublication(prepare, append);

    internal bool PublicationBridge => Descriptor.CanonicalName is "wf_act_send_signal" or "wf_act_neg_send_signal"
        or "wf_act_call_stacks" or "wf_act_neg_call_stacks";

    internal bool SupportsPublication(WiredRuntimeContext context)
    {
        var config = context.ConfigurationOf(this);
        var name = Descriptor.CanonicalName;

        if (!TryValidateConfiguration(config, out _, out _)) {
            return false;
        }

        if (PublicationBridge) {
            return true;
        }

        if (context.Policy.Addons.DisableAnimation) {
            return false;
        }

        string slot;

        if (name == "wf_act_move_to_dir" && config.IntParams.Length == 4
            && config.IntParams[0] is >= 0 and <= 7 && config.IntParams[1] == 0) {
            slot = "items";
        }
        else if (name == "wf_act_rel_mov" && config.IntParams.Length == 5) {
            slot = "movers";
        }
        // Observed on AIR WIN63-202610151745-318700621 (wf_act_furni_to_furni.spread): one firing's movers share one WiredMovements.
        else if (name == "wf_act_furni_to_furni") {
            slot = "movers";
        }
        // Observed on AIR WIN63-202610151745-318700621: all Move/Rotate boxes of one firing, with or without the projectile add-on,
        // share one WiredMovements.
        else if (name == "wf_act_move_rotate") {
            slot = "movers";
        }
        // Observed on AIR WIN63-202610151745-318700621: a receiver that fires twice in one tick sends one WiredMovements with one
        // entry per furni, from its tile at the start of the tick to where it ends, also when it ends where it started.
        else if (name == "wf_act_furni_to_user") {
            slot = "movers";
        }
        // Observed on AIR WIN63-202610151745-318700621 (r97_group_furni_clk): a group's moves also go out in one WiredMovements.
        else if (name == "wf_act_move_furni_as_group" && config.IntParams.Length == 6) {
            slot = "movers";
        }
        // Observed on AIR WIN63-202610201032-107964540: a firing that restores only the position of several furni, with or
        // without the altitude, sends all their moves in one WiredMovements.
        else if (name == "wf_act_match_to_sshot" && config.IntParams.Length == 5
            && config.IntParams.Take(3).SequenceEqual(new[] { 0, 0, 1 }) && config.IntParams[3] is 0 or 1
            && config.Snapshots.All(snapshot => snapshot.Wall == null)) {
            slot = "movers";
        }
        else {
            return false;
        }

        return Furni(context, config, slot).All(WiredRoomMovement.PlainPublicationItem);
    }

    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        // A mapped action has exactly one model: its bound native record. Unmapped names keep their runtime validator.
        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)) {
            if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)) {
                validated = proposed;
                error = "Invalid native editor authority.";

                return false;
            }

            return WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out validated, out error);
        }

        return TryValidateLegacyConfiguration(proposed, out validated, out error);
    }

    private bool TryValidateLegacyConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        var name = Descriptor.CanonicalName;

        if (name == "wf_act_give_reward") {
            return WiredRewards.TryValidate(proposed, out validated, out error);
        }

        if (name == "wf_act_teleport_to_room") {
            return WiredRoomForwarding.TryValidate(proposed, out validated, out error);
        }

        if (WiredBotActions.Names.Contains(name)) {
            return WiredBotActions.TryValidate(name, proposed, out validated, out error);
        }

        if (WiredMovementActions.Names.Contains(name)) {
            return WiredMovementConfiguration.TryValidate(name, proposed, out validated, out error);
        }

        validated = proposed;
        error = "Invalid action configuration.";

        if (!WiredLegacyProtocol.IsWithinLimits(proposed)) {
            return false;
        }

        var p = proposed.IntParams;
        bool F(int i) => p[i] is 0 or 100 or 200 or 201;
        bool U(int i) => p[i] is 0 or 10 or 11 or 200 or 201;
        var furni = ImmutableDictionary.CreateBuilder<string, int>();
        var users = ImmutableDictionary.CreateBuilder<string, int>();
        var secondary = proposed.SecondarySelectedItems;

        switch (name) {
            case "wf_act_join_team":
                if (p.Length != 4 || p[0] is < 0 or > 2 || p[1] is < 1 or > 4 || !U(2) || p[3] is < 0 or > 2) {
                    return false;
                }

                users["users"] = p[2];
                break;
            case "wf_act_leave_team":
            case "wf_act_kick_user":
                if (p.Length != 1 || !U(0)) {
                    return false;
                }

                users["users"] = p[0];
                break;
            case "wf_act_give_score":
            case "wf_act_give_score_tm":
                var newScoreShape = name == "wf_act_give_score" ? 4 : 5;

                if (p.Length != 3 && p.Length != newScoreShape || p[0] is < 1 or > 1000 || p[1] is < 0 or > 1
                    || (name == "wf_act_give_score" ? !U(2) : p[2] is < 1 or > 4)
                    || p.Length == newScoreShape && (p[^1] is < 0 or > 10 || name == "wf_act_give_score_tm" && !U(3))) {
                    return false;
                }

                if (p.Length == newScoreShape) {
                    proposed = proposed with { ScoreQuotaPerGame = p[^1] == 0 ? null : p[^1] };
                }

                if (name == "wf_act_give_score") {
                    users["users"] = p[2];
                }
                else if (p.Length == 5) {
                    users["users"] = p[3];
                }

                break;
            case "wf_act_mute_triggerer":
                if (p.Length != 2 || p[0] is < 0 or > 100000 || !U(1)) {
                    return false;
                }

                users["users"] = p[1];
                break;
            case "wf_act_freeze":
                if (p.Length != 3 || p[0] is not (0 or 218 or 12 or 11 or 53 or 163) || p[1] is < 0 or > 1 || !U(2)) {
                    return false;
                }

                users["users"] = p[2];
                break;
            case "wf_act_unfreeze":
                if (p.Length != 1 || !U(0)) {
                    return false;
                }

                users["users"] = p[0];
                break;
            case "wf_act_chase":
            case "wf_act_flee":
                if (p.Length != 1 || !F(0)) {
                    return false;
                }

                furni["items"] = p[0];
                break;
            case "wf_act_move_to_dir":
                if (p.Length != 4 || p[0] is < 0 or > 7 || p[1] is < 0 or > 6 || !F(2) || p[3] is < 0 or > 1) {
                    return false;
                }

                furni["items"] = p[2];
                break;
            case "wf_act_move_rotate_user":
                if (p.Length != 3 || p[0] is < -1 or > 7 || p[1] is < -1 or > 9 || !U(2)) {
                    return false;
                }

                users["users"] = p[2];
                break;
            case "wf_act_control_clock":
                if (p.Length != 2 || p[0] is < 0 or > 4 || !F(1)) {
                    return false;
                }

                furni["items"] = p[1];
                break;
            case "wf_act_adjust_clock":
                if (p.Length != 4 || p[0] is < 0 or > 2 || !F(1) || p[2] is < 0 or > 99 || p[3] is < 0 or > 119) {
                    return false;
                }

                furni["items"] = p[1];
                break;
            case "wf_act_reset_timers":
                if (p.Length != 0) {
                    return false;
                }

                furni["items"] = 900;
                break;
            case "wf_act_call_stacks":
            case "wf_act_neg_call_stacks":
                if (p.Length != 1 || !F(0)) {
                    return false;
                }

                furni["items"] = p[0];
                break;
            case "wf_act_log":
            case "wf_act_neg_log":
                if (p.Length != 2 || p[0] is < 0 or > 3 || !U(1)) {
                    return false;
                }

                users["users"] = p[1];
                break;
            case "wf_act_show_message":
                if (p.Length is not (3 or 4) || !U(0) || p[1] is < 0 or > 1 || p[2] is < 0 or > 1000
                    || p.Length == 4 && p[3] is < -1 or > 2) {
                    return false;
                }

                users["users"] = p[0];
                break;
            case "wf_act_click_conf":
                if (p.Length != 3 || p[0] is < 0 or > 2 || p[1] is < 0 or > 1 || !U(2)) {
                    return false;
                }

                users["users"] = p[2];
                break;
            case "wf_act_send_signal":
            case "wf_act_neg_send_signal":
                if (p.Length != 6 || p[0] < 0 || !F(1) || !U(2) || p[3] is < 0 or > 1 || p[4] is < 0 or > 1 || p[5] != 0) {
                    return false;
                }

                if (proposed.SelectedItems.Any(id => Instance.GetRoomItemHandler().GetItem(id) is { } receiver
                    && !WiredStackEngine.IsSignalAntenna(receiver))) {
                    error = "wiredfurni.error.require_antenna_furni";

                    return false;
                }

                var ids = ImmutableArray.CreateBuilder<uint>();

                foreach (var token in proposed.Text.Split([';', ',', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
                    if (ids.Count >= 100 || !uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id == 0) {
                        return false;
                    }

                    ids.Add(id);
                }

                secondary = ids.Distinct().ToImmutableArray();
                furni["items"] = 100;
                furni["forwarded"] = p[1];
                users["users"] = p[2];
                break;
            default:
                return false;
        }

        validated = proposed with { FurniSources = furni.ToImmutable(), UserSources = users.ToImmutable(), SecondarySelectedItems = secondary };
        error = "";

        return true;
    }
    public override bool Execute(WiredRuntimeContext context)
    {
        var config = context.ConfigurationOf(this);

        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)
            && !WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, config)) {
            return false;
        }

        if (!TryValidateConfiguration(config, out config, out _)) {
            return false;
        }

        var name = Descriptor.CanonicalName;

        if (name == "wf_act_give_reward") {
            return _rewards.Execute(Item, context, config);
        }

        if (WiredOctoberActions.Names.Contains(name)) {
            var native = config;

            return WiredOctoberActions.Execute(name, this, context, config, slot => Furni(context, native, slot), slot => Users(context, native, slot));
        }

        if (WiredTemporaryFurnitureActions.Supports(name)) {
            return WiredTemporaryFurnitureActions.Execute(name, Item, context, config, _definitions);
        }

        if (name == "wf_act_teleport_to_room") {
            return WiredRoomForwarding.Execute(context, config, _logger, _travelStore);
        }

        if (WiredBotActions.Names.Contains(name)) {
            return WiredBotActions.Execute(name, context, config, _movement, _botStore, (bot, item) => Teleport(context, bot, item, false));
        }

        if (WiredMovementActions.Names.Contains(name)) {
            var movers = config.FurniSources.ContainsKey("movers")
                ? Furni(context, config, "movers") : [];

            if (name is not ("wf_act_match_to_sshot" or "wf_act_set_altitude" or "wf_act_toggle_state" or "wf_act_toggle_to_rnd")) {
                movers = movers.Where(item => item.IsFloorItem).ToArray();
            }

            var flashedBox = false;

            if (name == "wf_act_toggle_state") {
                // Observed on AIR WIN63-202610201032-107964540: a box of the stack that is running has flashed already, and the toggle
                // flips it again, so it ends in the toggled state.
                var own = context.CapturedStack.Select(x => x.Item.Id).ToHashSet();
                movers = WiredToggleStateTargets.Resolve(movers, box => context.Room.GetWired().Flash(box, own.Contains(box.Id)),
                    item => context.Room.GetWired().TryQueueBatchData(item), out flashedBox);

                if (movers.Any(item => !WiredToggleStateTargets.WritesOnlyBatchData(item))) {
                    context.Publication?.Flush();
                }
            }

            if (name == "wf_act_toggle_to_rnd") {
                movers = WiredToggleStateTargets.ResolveRandom(movers);
            }

            var passengers = WiredRoomMovement.CapturePassengers(context, movers);

            var targets = config.FurniSources.ContainsKey("targets") ? Furni(context, config, "targets", name == "wf_act_furni_to_furni"
                || name == "wf_act_move_furni_to" && config.IntParams.Length == 4
                || name == "wf_act_move_furni_as_group" && config.IntParams.Length == 6) : [];

            if (name is "wf_act_move_furni_as_group" or "wf_act_teleport_to" && config.Origin?.Native != null) {
                targets = targets.Where(item => item.IsFloorItem).ToArray();
            }

            // Observed on hhus (AIR WIN63-202610131318-860371703): furni_to_furni with a wall item as its only target moves nothing.
            if (name == "wf_act_furni_to_furni") {
                targets = targets.Where(item => item.IsFloorItem).ToArray();
            }

            var movementUsers = config.UserSources.ContainsKey("users") ? Users(context, config, "users") : [];
            // Furniture sent to a user lands on that user's own tile, whichever selected user was picked; no other occupant is let through.
            IReadOnlySet<int>? landingUsers = name == "wf_act_furni_to_user" && movementUsers.Length > 0
                ? movementUsers.Select(user => user.VirtualId).ToHashSet() : null;

            var movedAny = new WiredMovementActions(_random).Execute(name, config, movers, targets, movementUsers,
                (item, x, y, rotation, height) => _movement.MoveFurniture(context, item, x, y, rotation, height,
                    WiredMovementActions.Steps.Contains(name)
                    && !(name == "wf_act_move_furni_to" && config.IntParams.Length == 4
                        || name == "wf_act_move_furni_as_group" && config.IntParams.Length == 6), passengers: passengers,
                    landingUsers: landingUsers),
                (user, target, slide, fast, walkMode) => slide
                    ? _movement.MoveAvatar(context, user, target.GetX, target.GetY, true, walkMode)
                    : Teleport(context, user, target, fast),
                (item, state) =>
                {
                    var mark = FurnitureStateEvents.Mark();
                    item.LegacyDataString = state;

                    context.Room.GetWired().PublishState(item, true);

                    if (FurnitureStateEvents.TakeWriteSince(item, mark)) {
                        _publish(new(WiredEventKind.StateChanged) { Actor = FurnitureStateEvents.Present(context.Room, context.Event.Actor), EventItem = item });
                    }
                },
                // v2 only: state toggles and snapshot restores go through the per-gate sequencer.
                GateTransitionService.For(Instance) == null ? null : (item, nextState) => GateTransitionService.ToggleState(item, nextState, GateCloseReason.Wired,
                    afterWrite: _ =>
                    {
                        // A queued write can land after the triggering user left: the change is still reported.
                        if (FurnitureStateEvents.TakeFollowedWrite(item)) {
                            _publish(new(WiredEventKind.StateChanged) { Actor = FurnitureStateEvents.Present(context.Room, context.Event.Actor), EventItem = item });
                        }
                    }) is GateTransition.Applied or GateTransition.Queued,
                // Observed on AIR WIN63-202610151745-318700621 (r97_mv_collide): a Move/Rotate step onto a stackable block lands on top of it
                // (z 1.0), also when that block arrived in the same firing; only furniture that cannot be stacked on blocks it.
                (movers, step) => _movement.MoveTogether(context, movers, step, passengers, WiredMovementActions.Steps.Contains(name),
                    stackableBlocks: name != "wf_act_move_rotate"), context.Room.GetGameMap().ValidTile,
                (item, snapshot, position, altitude) => _movement.RestoreWallSnapshot(context, item, snapshot, position, altitude),
                (item, operation, altitude) => _movement.SetWallAltitude(context, item, operation, altitude));

            // Observed on AIR WIN63-202610151745-318700621 (r97_group_furni_clk): a group's moves are listed in pick order, though the
            // landings are worked out leading edge first so members do not stack on each other.
            if (name == "wf_act_move_furni_as_group" && config.IntParams.Length == 6) {
                context.Publication?.Reorder(movers);
            }

            return movedAny || flashedBox;
        }

        // Reset timers always covers the whole room, unlimited, so it resolves its own targets.
        var items = name != "wf_act_reset_timers" && config.FurniSources.ContainsKey("items") ? Furni(context, config, "items") : [];

        if (name == "wf_act_move_to_dir") {
            // Dynamic sources may contain walls: exclude them before heading, collision or passenger work.
            items = items.Where(item => item.IsFloorItem).ToArray();
        }

        var carry = name is "wf_act_chase" or "wf_act_flee" or "wf_act_move_to_dir"
            ? WiredRoomMovement.CapturePassengers(context, items) : null;
        var changed = false;

        switch (name) {
            case "wf_act_join_team":
            case "wf_act_leave_team":
                context.Room.GetGameManager().Highscores.Invalidate();

                foreach (var user in Users(context, config, "users")) {
                    // The own avatar's effect change reaches the client on the next 500 ms state update, for joins and leaves alike.
                    void Deliver(Action apply)
                    {
                        if (!context.Room.GetWired().ScheduleAux(context, 0, apply, alignToStateUpdate: true)) {
                            apply();
                        }
                    }

                    // Observed on AIR WIN63-202610151745-318700621: a frozen user keeps the freeze effect when it joins or leaves a Wired,
                    // Battle Banzai or Freeze team, and unfreeze then shows the effect of its team at that time (223 Wired Red, 36 Battle
                    // Banzai Yellow, 40 Freeze Red), or 0 without a team.
                    var frozen = WiredAvatarState.For(context.Room);
                    var moved = name == "wf_act_join_team"
                        ? WiredGameState.For(context.Room).Join(context.Room, user, Param(config, 0), (Team)Param(config, 1), Param(config, 3), context.Targets.AllUsers(),
                            (member, team, effect) => Deliver(() =>
                            {
                                if (member.IsAttachedTo(context.Room) && member.Team == team
                                    && !frozen.SetEffectAfterThaw(member, _ => effect)) {
                                    member.ApplyEffect(effect);
                                }
                            }))
                        : WiredGameState.For(context.Room).Leave(context.Room, user, (member, effect) => Deliver(() =>
                        {
                            if (!member.IsAttachedTo(context.Room) || member.Team != Team.None
                                || frozen.SetEffectAfterThaw(member, previous => previous == effect ? 0 : previous)) {
                                return;
                            }

                            if (member.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == effect) {
                                member.ApplyEffect(0);
                            }
                        }));

                    // Observed on hhus (r90 game-b): YouArePlayingGame goes out with the join (true) and the leave (false), in the same
                    // tick and ahead of the team effect (223 on join, 0 on leave) of the next state update. Observed on AIR
                    // WIN63-202610151745-318700621: a leave sends it even outside a team, a join into the user's own team sends nothing.
                    if (moved || name == "wf_act_leave_team") {
                        user.GetClient()?.Send(new YouArePlayingGameComposer(name == "wf_act_join_team"));
                    }

                    changed |= moved;
                }

                return changed;
            case "wf_act_give_score":
            case "wf_act_give_score_tm":
                var amount = Param(config, 0) * (Param(config, 1) == 1 ? -1 : 1);

                if (name == "wf_act_give_score_tm" && config.IntParams.Length == 5) {
                    foreach (var user in Users(context, config, "users").Where(user => !user.IsBot)) {
                        changed |= WiredGameState.For(context.Room).GiveScore(context.Room, Item.Id, user.HabboId,
                            (Team)Param(config, 2), amount, config.ScoreQuotaPerGame, score => _publish(score with { Actor = user }));
                    }

                    return changed;
                }

                if (name == "wf_act_give_score_tm") {
                    var playerId = context.Event.Actor is { IsBot: false } actor ? actor.HabboId : 0;

                    if (config.ScoreQuotaPerGame.HasValue && playerId == 0) {
                        return false;
                    }

                    return WiredGameState.For(context.Room).GiveScore(context.Room, Item.Id, playerId, (Team)Param(config, 2), amount, config.ScoreQuotaPerGame, _publish);
                }

                foreach (var user in Users(context, config, "users").Where(user => !user.IsBot)) {
                    changed |= WiredGameState.For(context.Room).GiveScore(context.Room, Item.Id, user.HabboId, user.Team, amount, config.ScoreQuotaPerGame,
                        score => _publish(score with { Actor = user }));
                }

                return changed;
            case "wf_act_kick_user":
            case "wf_act_mute_triggerer":
                DateTimeOffset mutedUntil = default;
                var now = name == "wf_act_mute_triggerer" ? _clock.GetUtcNow() : default;

                if (name == "wf_act_mute_triggerer" && !RoomMuteDeadline.TryCreate(now, Param(config, 0), out mutedUntil)) {
                    return false;
                }

                foreach (var user in Users(context, config, "users").Where(user => !user.IsBot)) {
                    var client = user.GetClient();
                    var player = client?.GetHabbo();

                    if (player == null || client == null) {
                        continue;
                    }

                    if (player.Id == context.Room.OwnerId) {
                        // Observed on hhus (AIR WIN63-202610131318-860371703): the owner is not kicked and is whispered this notification.
                        if (name == "wf_act_kick_user") {
                            client.Send(new WiredChatComposer(user.VirtualId, user.VirtualId, KickOwnerException, 34, -1, true));
                        }

                        continue;
                    }

                    if (player.Access.Can(PermissionKeys.ModerationTool)) {
                        continue;
                    }

                    // Observed on AIR WIN63-202610201032-107964540: Kick User whispers the user even when the box has no message (an empty line).
                    if (name == "wf_act_kick_user" || config.Text.Length > 0) {
                        client.Send(new WiredChatComposer(user.VirtualId, user.VirtualId, FormatLegacyText(context, user, config.Text), 34, -1, true));
                    }

                    if (name == "wf_act_kick_user") {
                        Kick(context, user, client);
                    }
                    else {
                        context.Room.MutedUsers[player.Id] = mutedUntil;
                    }

                    changed = true;
                }

                return changed;
            case "wf_act_freeze":
            case "wf_act_unfreeze":
                var avatarState = WiredAvatarState.For(context.Room);

                foreach (var user in Users(context, config, "users")) {
                    changed |= name == "wf_act_freeze" ? avatarState.FreezeUser(user, Param(config, 0), Param(config, 1) == 1) : avatarState.Thaw(user);
                }

                return changed;
            case "wf_act_chase":
            case "wf_act_flee":
                _directions.Retain(context.Targets.AllFurni());

                foreach (var item in items) {
                    changed |= ChaseOrFlee(item);
                }

                return changed;

                bool ChaseOrFlee(Item item)
                {
                    bool Move(int x, int y) => _movement.MoveFurniture(context, item, x, y, item.Rotation, null, passengers: carry);
                    var flee = name == "wf_act_flee";
                    // Observed r92f r92-flee: flee moves with any user in the room (Manhattan 10 still moves on all three fires).
                    // Observed on AIR WIN63-202610151745-318700621: chase only follows a user in its own row or column (2 and 3 tiles away it
                    // stepped towards the user 10 times in 10; at offsets (1,1), (2,1), (2,2) and (3,1) it took a random step). Flee
                    // steps straight away only from such a user, too.
                    var random = _random ?? Random.Shared;
                    var inLine = WiredDirectionalActions.Nearest(item, context.Targets.AllUsers().Where(user => user.X == item.GetX || user.Y == item.GetY), 3, random);
                    var nearest = flee ? WiredDirectionalActions.Nearest(item, context.Targets.AllUsers(), null, random) : inLine;

                    if (nearest == null) {
                        if (flee) {
                            return false;
                        }

                        // Observed r92j r92h-chase-retry (AIR WIN63-202610131318-860371703): from the corner (4,1), with north and west
                        // void, the out-of-range marker moved on all six trials (east 3, south 3), so a blocked random direction is retried.
                        // Observed on AIR WIN63-202610151745-318700621: the random step never goes straight back (no reversal in 54 steps).
                        int[] headings = [0, 2, 4, 6];
                        Random.Shared.Shuffle(headings);
                        var back = _directions.LastChaseStep(item) is { } last ? (last + 4) % 8 : -1;

                        foreach (var heading in headings.OrderBy(heading => heading == back)) {
                            var step = Rotation.Offset(heading);

                            if (Move(item.GetX + step.X, item.GetY + step.Y)) {
                                _directions.RememberChaseStep(item, heading);

                                return true;
                            }
                        }

                        return false;
                    }

                    if (Math.Abs(nearest.X - item.GetX) + Math.Abs(nearest.Y - item.GetY) <= 1) {
                        _publish(new(WiredEventKind.Collision) { Actor = nearest, EventItem = item });

                        return true;
                    }

                    var candidates = flee ? WiredDirectionalActions.FleeSteps(item, inLine, nearest).ToArray()
                        : WiredDirectionalActions.Steps(item, nearest, false).Take(1).ToArray();
                    var (fromX, fromY) = (item.GetX, item.GetY);

                    foreach (var candidate in candidates) {
                        if (Move(candidate.X, candidate.Y)) {
                            if (!flee) {
                                _directions.RememberChaseStep(item, Enumerable.Range(0, 4).Select(index => index * 2)
                                    .First(heading => Rotation.Offset(heading) == new System.Drawing.Point(candidate.X - fromX, candidate.Y - fromY)));
                            }

                            return true;
                        }
                    }

                    return false;
                }
            case "wf_act_move_to_dir":
                _directions.Retain(context.Targets.AllFurni());

                foreach (var item in items) {
                    changed |= _directions.MoveHeading(item, Param(config, 0), HeadingTurnRule(Param(config, 1)), Param(config, 3) == 1,
                        (x, y) => _movement.MoveFurniture(context, item, x, y, item.Rotation, null, passengers: carry),
                        (x, y) => context.Room.GetGameMap().ValidTile(x, y) ? context.Room.GetGameMap().GetRoomUsers(new(x, y)).ToArray() : [],
                        (furni, actor) =>
                        {
                            var collision = new WiredRuntimeEvent(WiredEventKind.Collision) { Actor = actor, EventItem = furni };

                            if (ReferenceEquals(_publish, _headingPublisher) && _headingCollision?.Invoke(context, collision) is not null) {
                                return;
                            }

                            context.Publication?.Flush();
                            _publish(collision);
                        }, context.Policy.Addons.Physics?.ThroughUsers);
                }

                return changed;
            case "wf_act_move_rotate_user":
                // Observed on AIR WIN63-202610201032-107964540: one box sends one WiredMovements with the moves of all its users, then
                // their turns.
                var moves = new List<WiredMovementComposer>();
                var turns = new List<WiredUserDirectionComposer>();

                foreach (var user in Users(context, config, "users")) {
                    if (Param(config, 0) >= 0) {
                        var offset = Rotation.Offset(Param(config, 0));
                        // Observed on AIR WIN63-202610131318-860371703: a push on a walking user replaces the step in progress in the same tick.
                        // It runs from the step's start tile to the stepped-onto tile plus the push, then the walk resumes towards its goal
                        // (pushed back onto the start tile, the user treadmills). Pushed on the last step, the walk ends there.
                        var stepping = user.IsWalking && user.SetStep;
                        var (baseX, baseY) = stepping ? (user.SetX, user.SetY) : (user.X, user.Y);
                        var resume = stepping && (user.GoalX, user.GoalY) != (baseX, baseY);
                        changed |= _movement.MoveAvatar(context, user, baseX + offset.X, baseY + offset.Y, true, resume ? 1 : 2, replaceStep: stepping,
                            batch: moves);
                    }

                    if (Param(config, 1) < 0) {
                        continue;
                    }

                    var rotation = WiredDirectionalActions.AvatarRotation(user.RotBody, Param(config, 1));

                    // Observed on AIR WIN63-202610201032-107964540: a user on a seat or bed keeps the furni direction. The turn is still
                    // sent, and the status update that follows shows the old direction again. A user who sits on the floor turns.
                    if (user.Statusses.ContainsKey("lay") || user.Statusses.ContainsKey("sit") && !user.IsSitting) {
                        user.UpdateNeeded = true;
                    }
                    // The direction entry is sent even when the avatar already faces that way; only a real change needs a status update.
                    else if (user.RotBody != rotation || user.RotHead != rotation) {
                        user.RotBody = rotation;
                        user.RotHead = rotation;
                        user.UpdateNeeded = true;
                    }

                    turns.Add(new WiredUserDirectionComposer(user.VirtualId, rotation, rotation));
                    changed = true;
                }

                if (moves.Count != 0 || turns.Count != 0) {
                    // Observed on AIR WIN63-202610201032-107964540: the chat of a Show Message in the same stack follows the WiredMovements, whichever
                    // box comes first and with or without Execute in Order (75 of 96 firings; in the others the chat came first in the same
                    // millisecond), so the held messages stay until the pass ends.
                    context.Room.SendWiredMovement(new WiredMovementBatchComposer(moves, turns));
                }

                return changed;
            case "wf_act_control_clock":
                foreach (var item in items) {
                    changed |= _clocks.Control(item, Param(config, 0), context.NowMilliseconds, WiredClockOrigin.ModernWired, context.Event.Actor);
                }

                return changed;
            case "wf_act_adjust_clock":
                foreach (var item in items) {
                    changed |= _clocks.Adjust(item, Param(config, 0), Param(config, 2), Param(config, 3), WiredClockOrigin.ModernWired,
                        context.NowMilliseconds);
                }

                return changed;
            case "wf_act_reset_timers":
                var resetAt = _clock.GetUtcNow();
                context.Room.RestartTimerAt(resetAt,
                    resetAt.AddMilliseconds(Triggers.WiredTimedTriggers.FirstRoomTickAfter(context.NowMilliseconds) - context.NowMilliseconds));
                // The room timer belongs to the whole room; a furni limit add-on must not pick which timers restart.
                context.Operations.ResetTimers(context.Targets.ResolveFurni(context, [], WiredSources.AllRoom, raw: true));

                return true;
            case "wf_act_call_stacks":
            case "wf_act_neg_call_stacks":
                return context.Operations.CallStacks(context, items.Where(item => item.GetX != Item.GetX || item.GetY != Item.GetY), IsNegative);
            case "wf_act_send_signal":
            case "wf_act_neg_send_signal":
                var forwarded = Furni(context, config, "forwarded", true).Select(item => item.Id).ToArray();
                var users = Users(context, config, "users").Select(user => user.VirtualId).ToArray();
                var furniBatches = Param(config, 3) == 1 ? forwarded.Select(id => new[] { id }).ToArray() : [forwarded];
                // Observed on AIR WIN63-202610151745-318700621: split user signals run in a new random order each firing, whatever
                // the selection order.
                var userBatches = Param(config, 4) == 1 ? users.Select(id => new[] { id }).ToArray() : [users];
                (_random ?? Random.Shared).Shuffle(userBatches);

                foreach (var furni in furniBatches) {
                    foreach (var avatars in userBatches) {
                        changed |= context.Operations.SendSignal(context, items, new(furni, avatars), IsNegative);
                    }
                }

                return changed;
            case "wf_act_log":
            case "wf_act_neg_log":
                if (config.Text.Length == 0) {
                    return false;
                }

                var message = context.Policy.FormatText(context, config.Text);
                _roomLog.Append(Param(config, 0), WiredLogSource.WiredLog, Item.Id, Descriptor.CanonicalName, message, _clock.GetUtcNow());
                _logger.Log(Param(config, 0) switch { 0 => LogLevel.Information, 1 => LogLevel.Warning, 2 => LogLevel.Error, _ => LogLevel.Debug }, "{Message}", message);

                return true;
            case "wf_act_show_message":
                if (config.Text.Length == 0) {
                    return false;
                }

                foreach (var user in Users(context, config, "users")) {
                    var client = user.GetClient();
                    var shownToRoom = Param(config, 1) != 0;

                    // Observed on hhus (AIR WIN63-202610131318-860371703): a message shown to everyone for a bot is said by the bot
                    // (Bot Reached Furni/Avatar + Show Message). A private one has nobody to go to.
                    if (client == null && !(user.IsBot && shownToRoom)) {
                        continue;
                    }

                    if (!WiredChatRate.For(context.Room).TryAdmit(context.NowMilliseconds)) {
                        continue;
                    }

                    var packet = new WiredChatComposer(user.VirtualId, user.VirtualId, FormatLegacyText(context, user, config.Text), Param(config, 2), Param(config, 3, -1), !shownToRoom);

                    var room = context.Room;
                    var target = client;
                    // Observed on AIR WIN63-202610201032-107964540: a bot saying the message to everyone turns heads like its own chat.
                    var turnsHeads = shownToRoom && user.IsBot && !user.IsPet;

                    room.GetWired().AfterFlash(() =>
                    {
                        if (packet.Private) {
                            target!.Send(packet);
                        }
                        else {
                            room.SendChat(receiver => packet with { ReceiverVirtualId = receiver });
                        }

                        if (turnsHeads) {
                            room.GetRoomUserManager().TurnHeadsToward(user);
                        }

                        // Observed on AIR WIN63-202610201032-107964540 (4 of 4 runs for each visibility): a Show Message wakes a sleeping
                        // user on the next room tick, as their own chat does.
                        if (!user.IsBot) {
                            user.UnIdle(atRoomTick: true);
                        }
                    }, context);

                    changed = true;
                }

                return changed;
            case "wf_act_click_conf":
                foreach (var user in Users(context, config, "users")) {
                    var client = user.GetClient();

                    if (client == null) {
                        continue;
                    }

                    client.Send(new WiredClickSettingsComposer(Param(config, 0), Param(config, 1)));
                    changed = true;
                }

                return changed;
            default:
                throw new InvalidOperationException("Action has no executor.");
        }
    }

    /// <summary>
    /// The furniture steps this box would take now, for wf_cnd_valid_moves: rel_mov, a fixed move_rotate direction and
    /// move_to_dir's current heading. Random and target moves are not planned.
    /// </summary>
    internal IEnumerable<(Item Item, int X, int Y, int Rotation, bool Step)> PlannedMoves(WiredRuntimeContext context)
    {
        var config = context.ConfigurationOf(this);
        var name = Descriptor.CanonicalName;

        if (name is not ("wf_act_rel_mov" or "wf_act_move_rotate" or "wf_act_move_to_dir")) {
            yield break;
        }

        var slot = name == "wf_act_move_to_dir" ? "items" : "movers";

        if (!config.FurniSources.ContainsKey(slot)) {
            yield break;
        }

        foreach (var item in Furni(context, config, slot).Where(item => item.IsFloorItem)) {
            switch (name) {
                case "wf_act_rel_mov":
                    var dx = (Param(config, 0, 1) == 0 ? -1 : 1) * Param(config, 1);
                    var dy = (Param(config, 2, 1) == 0 ? -1 : 1) * Param(config, 3);

                    if (dx != 0 || dy != 0) {
                        yield return (item, item.GetX + dx, item.GetY + dy, item.Rotation, false);
                    }

                    break;
                case "wf_act_move_rotate":
                    if (Param(config, 0, -1) is >= 0 and <= 7 and var direction) {
                        var offset = Rotation.Offset(direction);
                        yield return (item, item.GetX + offset.X, item.GetY + offset.Y, item.Rotation, true);
                    }

                    break;
                default:
                    var heading = Rotation.Offset(_directions.Heading(item, Param(config, 0)));
                    yield return (item, item.GetX + heading.X, item.GetY + heading.Y, item.Rotation, false);
                    break;
            }
        }
    }

    // Editor choice -> turn rule. Observed r92d r92-mtd-wall (mover heading north into the void): right 45/90 end east, left 45/90
    // west, back south, all in the same fire, and the new heading is kept on the next fire; wait stays.
    private static int HeadingTurnRule(int choice) => choice switch
    {
        0 => 6,
        1 => 3,
        2 => 1,
        3 => 4,
        4 => 2,
        5 => 0,
        6 => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };

    private bool Teleport(WiredRuntimeContext context, RoomUser user, Item target, bool fast)
    {
        // A freeze that ends on teleport ends before the move, including when the user is already there.
        WiredAvatarState.For(context.Room).Thaw(user, true, out var effectAfterFreeze);

        if (!context.Targets.ResolveFurni(context, [target.Id], 100, raw: true).Any(attached => ReferenceEquals(attached, target))) {
            return false;
        }

        // A user already on the target is teleported all the same. Observed on AIR WIN63-202610151745-318700621
        // (wf_xtra_filter_users.area_teleport): the owner standing on T is republished at +1.5 s and relocated onto T at +2.0 s.
        var room = context.Room;

        bool Relocate()
        {
            var (x, y) = TeleportLanding(room, user, target);

            return _movement.MoveAvatar(context, user, x, y, false, 2, ignoreOccupants: true);
        }

        var effects = user.GetClient()?.GetHabbo()?.Effects;

        if (!user.IsBot && effects == null) {
            return Relocate();
        }

        // Observed on AIR WIN63-202610131318-860371703: every step counts in room ticks from the first tick after the firing.
        // Normal: effect 4 at the firing, the avatar republished on the 3rd tick, relocated on the 4th and the effect dropped on the 7th
        // (1500 ms after the landing); the walk is not stopped. Fast: effect 235, the walk ends on the tile being stepped onto, the avatar
        // is republished on the 1st tick, lands on the 2nd and the effect drops on the 3rd. A fast teleport from a walk-off stack fired
        // with a step's announce arrives on the next tick and lands on the one after.
        // Each step runs at the start of its tick's room pass, so it goes out with that tick's avatar updates, whatever the phase of the
        // firing against the 500 ms Wired clock.
        var wired = room.GetWired();
        var firstTick = wired.FirstRoomTickAfter(context.NowMilliseconds);
        var tick = WiredFlashStates.StateUpdateMilliseconds;
        var (republish, relocate, effectEnd) = fast
            ? (firstTick, firstTick + tick, firstTick + 2 * tick)
            : (firstTick + 2 * tick, firstTick + 3 * tick, firstTick + 6 * tick);
        var release = () => { };

        // Nothing is applied unless the firing can hold its three scheduled steps; the effect starts with the first one.
        if (!wired.ScheduleOnRoomTick(context, republish, () =>
            {
                if (!user.IsAttachedTo(room)) {
                    return;
                }

                user.UpdateNeeded = true;

                // Observed on AIR WIN63-202610201032-107964540: Bot Teleport fires Bot Reaches Furni with the republish, before the landing.
                if (user is { IsBot: true, IsPet: false }) {
                    var (x, y) = TeleportLanding(room, user, target);

                    foreach (var item in room.GetGameMap().GetCoordinatedItems(new(x, y))) {
                        wired.Dispatch(new(WiredEventKind.BotReachedFurni) { Actor = user, EventItem = item });
                    }
                }
            }, () => release())) {
            return Relocate();
        }

        var restore = WiredTemporaryEffects.For(room).Acquire(user, () => user.CurrentEffect, user.ApplyEffect, () => user.IsAttachedTo(room),
            fast ? FastTeleportEffectId : TeleportEffectId, effectAfterFreeze);
        release = restore;

        if (fast) {
            StopAfterCurrentStep(user);
        }

        if (!wired.ScheduleOnRoomTick(context, relocate, () =>
            {
                if (user.IsAttachedTo(room)) {
                    Relocate();
                }
            })) {
            restore();

            return Relocate();
        }

        // Observed on AIR WIN63-202610151745-318700621: when the glow ends on a tick that also sends the avatar's status update, the
        // status update goes first and the effect change follows it.
        if (!wired.ScheduleOnRoomTick(context, effectEnd, restore, restore, afterAvatarUpdates: true)) {
            restore();
        }

        return true;
    }

    // Observed on AIR WIN63-202610131318-860371703: a teleport lands past users standing on the target, and onto furni nobody can stand on
    // it does not land: the user lands on a free tile next to it (the tile north of the target was taken; the order of the other
    // neighbours is not established).
    private static readonly (int X, int Y)[] TeleportNeighbours = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private static (int X, int Y) TeleportLanding(Room room, RoomUser user, Item target)
    {
        var map = room.GetGameMap();
        var (x, y) = (target.GetX, target.GetY);

        if (!map.ValidTile(x, y) || map.Model.SqState[x, y] != SquareState.Open || WiredRoomOperations.AvatarCanStand(map, x, y)) {
            return (x, y);
        }

        foreach (var (dx, dy) in TeleportNeighbours) {
            var (nx, ny) = (x + dx, y + dy);

            if (map.ValidTile(nx, ny) && map.Model.SqState[nx, ny] == SquareState.Open && WiredRoomOperations.AvatarCanStand(map, nx, ny)
                && !map.GetRoomUsers(new(nx, ny)).Any(other => !ReferenceEquals(other, user))) {
                return (nx, ny);
            }
        }

        return (x, y);
    }

    // The step in progress still lands; the walk ends there.
    private static void StopAfterCurrentStep(RoomUser user)
    {
        if (!user.IsWalking) {
            return;
        }

        if (user.SetStep) {
            user.MoveTo(user.SetX, user.SetY, MoveOrigin.Wired);
        }
        else {
            user.ClearMovement(true);
        }
    }

    // Observed on hhus: the kick applies avatar effect 4 on the next 500 ms state update and removes the user 2000 ms after the firing.
    // Observed on AIR WIN63-202610201032-107964540: the removal sends no GenericError and closes with the short 2.
    private void Kick(WiredRuntimeContext context, RoomUser user, GameClient client)
    {
        var room = context.Room;
        var wired = room.GetWired();

        void Remove()
        {
            if (user.IsAttachedTo(room)) {
                room.GetRoomUserManager().RemoveUserFromRoom(client, true, closeReason: KickCloseReason);
            }
        }

        if (!wired.ScheduleAux(context, KickRemoveMilliseconds, Remove)) {
            Remove();

            return;
        }

        if (!wired.ScheduleAux(context, 0, () =>
            {
                if (user.IsAttachedTo(room)) {
                    user.ApplyEffect(KickEffectId);
                }
            }, alignToStateUpdate: true)) {
            user.ApplyEffect(KickEffectId);
        }
    }

    private const int KickEffectId = 4;
    private const int KickRemoveMilliseconds = 2000;
    private const short KickCloseReason = 2;
    private const int TeleportEffectId = 4;
    private const int FastTeleportEffectId = 235;

    // Observed on hhus: a "<...>" segment of Show Message text is removed (brackets included) at output; the stored text is untouched.
    // Unknown $(name) placeholders are left literal by the text formatters.
    internal static string StripAngleSegments(string text)
    {
        string previous;

        do {
            previous = text;
            text = AngleSegment.Replace(text, "");
        } while (text != previous);

        return text;
    }

    private static readonly System.Text.RegularExpressions.Regex AngleSegment = new("<[^<>]*>", System.Text.RegularExpressions.RegexOptions.Compiled);

    private string FormatLegacyText(WiredRuntimeContext context, RoomUser user, string text) =>
        context.Policy.FormatText(context, StripAngleSegments(text).Replace("%USERNAME%", user.GetUsername(), StringComparison.Ordinal)
            .Replace("%ROOMNAME%", context.Room.Name ?? "", StringComparison.Ordinal)
            .Replace("%USERCOUNT%", context.Room.UserCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("%USERSONLINE%", _clients.Count.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal));
}
