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
    private readonly Random? _movementRandom;
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
        _movementRandom = movementRandom;
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

        if (context.Policy.Addons.Projectile != null || context.Policy.Addons.DisableAnimation) {
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
        else if (name == "wf_act_match_to_sshot" && config.IntParams.Length == 5
            && config.IntParams.Take(4).SequenceEqual(new[] { 0, 0, 1, 1 })
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
            // Furniture sent to a user lands on that user's own tile; no other occupant is let through.
            IReadOnlySet<int>? landingUsers = name == "wf_act_furni_to_user" && movementUsers.Length > 0
                ? new HashSet<int> { movementUsers[0].VirtualId } : null;

            return new WiredMovementActions(_movementRandom).Execute(name, config, movers, targets, movementUsers,
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
                (movers, step) => _movement.MoveTogether(context, movers, step, passengers, WiredMovementActions.Steps.Contains(name)), context.Room.GetGameMap().ValidTile,
                (item, snapshot, position, altitude) => _movement.RestoreWallSnapshot(context, item, snapshot, position, altitude),
                (item, operation, altitude) => _movement.SetWallAltitude(context, item, operation, altitude));
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

                    var moved = name == "wf_act_join_team"
                        ? WiredGameState.For(context.Room).Join(context.Room, user, Param(config, 0), (Team)Param(config, 1), Param(config, 3), context.Targets.AllUsers(),
                            (member, team, effect) => Deliver(() =>
                            {
                                if (member.IsAttachedTo(context.Room) && member.Team == team) {
                                    member.ApplyEffect(effect);
                                }
                            }))
                        : WiredGameState.For(context.Room).Leave(context.Room, user, (member, effect) => Deliver(() =>
                        {
                            if (member.IsAttachedTo(context.Room) && member.Team == Team.None
                                && member.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == effect) {
                                member.ApplyEffect(0);
                            }
                        }));

                    // Observed on hhus (r90 game-b): YouArePlayingGame goes out with the join (true) and the leave (false), in the same
                    // tick and ahead of the team effect (223 on join, 0 on leave) of the next state update.
                    if (moved) {
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
                            client.Send(new WiredChatComposer(user.VirtualId, KickOwnerException, 34, -1, true));
                        }

                        continue;
                    }

                    if (player.Access.Can(PermissionKeys.ModerationTool)) {
                        continue;
                    }

                    if (config.Text.Length > 0) {
                        client.Send(new WiredChatComposer(user.VirtualId, FormatLegacyText(context, user, config.Text), 34, -1, true));
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
                foreach (var item in items) {
                    changed |= ChaseOrFlee(item);
                }

                return changed;

                bool ChaseOrFlee(Item item)
                {
                    bool Move(int x, int y) => _movement.MoveFurniture(context, item, x, y, item.Rotation, null, passengers: carry);
                    var flee = name == "wf_act_flee";
                    // Observed r92f r92-flee: flee has no range (Manhattan 10 still moves away on all three fires); chase keeps its range.
                    var nearest = WiredDirectionalActions.Nearest(item, context.Targets.AllUsers(), flee ? null : 3);

                    if (nearest == null) {
                        if (flee) {
                            return false;
                        }

                        // Observed r92j r92h-chase-retry (AIR WIN63-202610131318-860371703): from the corner (4,1), with north and west
                        // void, the out-of-range marker moved on all six trials (east 3, south 3), so a blocked random direction is retried.
                        int[] headings = [0, 2, 4, 6];
                        Random.Shared.Shuffle(headings);

                        return headings.Select(WiredRoomOperations.Offset).Any(step => Move(item.GetX + step.X, item.GetY + step.Y));
                    }

                    if (Math.Abs(nearest.X - item.GetX) + Math.Abs(nearest.Y - item.GetY) <= 1) {
                        _publish(new(WiredEventKind.Collision) { Actor = nearest, EventItem = item });

                        return true;
                    }

                    var candidates = flee ? WiredDirectionalActions.FleeSteps(item, nearest).ToArray()
                        : WiredDirectionalActions.Steps(item, nearest, false).Take(1).ToArray();

                    return candidates.Any(candidate => Move(candidate.X, candidate.Y));
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
                foreach (var user in Users(context, config, "users")) {
                    if (Param(config, 0) >= 0) {
                        var offset = WiredRoomOperations.Offset(Param(config, 0));
                        // Observed on AIR WIN63-202610131318-860371703: a push on a walking user replaces the step in progress in the same tick.
                        // It runs from the step's start tile to the stepped-onto tile plus the push, then the walk resumes towards its goal
                        // (pushed back onto the start tile, the user treadmills). Pushed on the last step, the walk ends there.
                        var stepping = user.IsWalking && user.SetStep;
                        var (baseX, baseY) = stepping ? (user.SetX, user.SetY) : (user.X, user.Y);
                        var resume = stepping && (user.GoalX, user.GoalY) != (baseX, baseY);
                        changed |= _movement.MoveAvatar(context, user, baseX + offset.X, baseY + offset.Y, true, resume ? 1 : 2, replaceStep: stepping);
                    }

                    if (Param(config, 1) < 0) {
                        continue;
                    }

                    var rotation = WiredDirectionalActions.AvatarRotation(user.RotBody, Param(config, 1));

                    // The direction entry is sent even when the avatar already faces that way; only a real change needs a status update.
                    if (user.RotBody != rotation || user.RotHead != rotation) {
                        user.RotBody = rotation;
                        user.RotHead = rotation;
                        user.UpdateNeeded = true;
                    }

                    context.Room.SendPacket(new WiredUserDirectionComposer(user.VirtualId, rotation, rotation));
                    changed = true;
                }

                return changed;
            case "wf_act_control_clock":
                foreach (var item in items) {
                    changed |= _clocks.Control(item, Param(config, 0), context.NowMilliseconds, WiredClockOrigin.ModernWired, context.Event.Actor);
                }

                return changed;
            case "wf_act_adjust_clock":
                foreach (var item in items) {
                    changed |= _clocks.Adjust(item, Param(config, 0), Param(config, 2), Param(config, 3),
                        Param(config, 0) == 2 ? WiredClockOrigin.ModernWired : WiredClockOrigin.Unknown);
                }

                return changed;
            case "wf_act_reset_timers":
                context.Room.LastTimerResetAt = _clock.GetUtcNow();
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
                var furniBatches = Param(config, 3) == 1 && forwarded.Length != 0 ? forwarded.Select(id => new[] { id }).ToArray() : [forwarded];
                var userBatches = Param(config, 4) == 1 && users.Length != 0 ? users.Select(id => new[] { id }).ToArray() : [users];

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

                    if (client == null) {
                        continue;
                    }

                    var packet = new WiredChatComposer(user.VirtualId, FormatLegacyText(context, user, config.Text), Param(config, 2), Param(config, 3, -1), Param(config, 1) == 0);

                    if (packet.Private) {
                        client.Send(packet);
                    }
                    else {
                        context.Room.SendPacket(packet);
                    }

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
                        var offset = WiredRoomOperations.Offset(direction);
                        yield return (item, item.GetX + offset.X, item.GetY + offset.Y, item.Rotation, true);
                    }

                    break;
                default:
                    var heading = WiredRoomOperations.Offset(_directions.Heading(item, Param(config, 0)));
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
        WiredAvatarState.For(context.Room).Thaw(user, teleport: true);

        if (!context.Targets.ResolveFurni(context, [target.Id], 100, raw: true).Any(attached => ReferenceEquals(attached, target))) {
            return false;
        }

        if (user.X == target.GetX && user.Y == target.GetY) {
            return true;
        }

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

        // Observed on AIR WIN63-202610131318-860371703: every step counts from the first 500 ms room boundary strictly after the firing.
        // Normal: effect 4 at the firing, the avatar republished 1000 ms after that boundary, relocated 500 ms later and the effect
        // dropped 1500 ms after the landing (an on-boundary firing lands at +2.0 s, effect 0 at +3.5 s); the walk is not stopped.
        // Fast: effect 235, the walk ends on the tile being stepped onto, the landing is on the next boundary (+1000 ms for an
        // on-boundary firing) and the effect drops 500 ms after it.
        var firstBoundary = (int)(NextStateUpdate(context.NowMilliseconds) - context.NowMilliseconds);
        var (republish, relocate, effectEnd) = fast
            ? (firstBoundary, firstBoundary + FastTeleportRelocateMilliseconds, firstBoundary + FastTeleportEffectEndMilliseconds)
            : (firstBoundary + TeleportRepublishMilliseconds, firstBoundary + TeleportRelocateMilliseconds, firstBoundary + TeleportEffectEndMilliseconds);
        var wired = room.GetWired();
        var release = () => { };
        // The steps are scheduled from the engine's clock now, which can be a few ms past the firing's time; without this a step due
        // on a boundary rounded up to the next one, so fast teleports landed a tick late on our hotel.
        var lag = (int)Math.Max(0, wired.NowMilliseconds - context.NowMilliseconds);
        republish -= lag;
        relocate -= lag;
        effectEnd -= lag;
        // The avatar's update goes out with the room tick after the step runs (the room serializes users before Wired runs), so the
        // republish and the landing run one tick ahead of the tick they show on. A fast republish due on the first boundary runs now.
        var tick = WiredFlashStates.StateUpdateMilliseconds;

        // Nothing is applied unless the firing can hold its three scheduled steps; the effect starts with the first one.
        if (!wired.ScheduleAux(context, Math.Max(0, republish - tick), () =>
            {
                if (user.IsAttachedTo(room)) {
                    user.UpdateNeeded = true;
                }
            }, () => release(), alignToStateUpdate: republish > tick)) {
            return Relocate();
        }

        var restore = WiredTemporaryEffects.For(room).Acquire(user, () => user.CurrentEffect, user.ApplyEffect, () => user.IsAttachedTo(room),
            fast ? FastTeleportEffectId : TeleportEffectId);
        release = restore;

        if (fast) {
            StopAfterCurrentStep(user);
        }

        if (!wired.ScheduleAux(context, relocate - tick, () =>
            {
                if (user.IsAttachedTo(room)) {
                    Relocate();
                }
            }, alignToStateUpdate: true)) {
            restore();

            return Relocate();
        }

        if (!wired.ScheduleAux(context, effectEnd, restore, restore, alignToStateUpdate: true)) {
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

    private static long NextStateUpdate(long nowMilliseconds) =>
        (nowMilliseconds / WiredFlashStates.StateUpdateMilliseconds + 1) * WiredFlashStates.StateUpdateMilliseconds;

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
    private void Kick(WiredRuntimeContext context, RoomUser user, GameClient client)
    {
        var room = context.Room;
        var wired = room.GetWired();

        void Remove()
        {
            if (user.IsAttachedTo(room)) {
                room.GetRoomUserManager().RemoveUserFromRoom(client, true, true);
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
    private const int TeleportEffectId = 4;
    private const int FastTeleportEffectId = 235;
    private const int TeleportRepublishMilliseconds = 1000;
    private const int TeleportRelocateMilliseconds = 1500;
    private const int TeleportEffectEndMilliseconds = 3000;
    private const int FastTeleportRelocateMilliseconds = 500;
    private const int FastTeleportEffectEndMilliseconds = 1000;

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
