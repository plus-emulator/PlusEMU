using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Items.Wired.Modern.Conditions;

public sealed class WiredModernCondition : WiredModernBox
{
    private readonly Func<Item, long?> _counterTime;
    private readonly Func<DateTimeOffset> _clock;
    private readonly IGroupManager _groups;
    public WiredModernCondition(Room room, Item item, WiredBoxDescriptor descriptor,
        IGroupManager groups, Func<Item, long?> counterTime, Func<DateTimeOffset> clock) : base(room, item, descriptor)
    {
        if (!WiredConditionConfiguration.Supports(descriptor.CanonicalName)) {
            throw new ArgumentException("Unknown condition.", nameof(descriptor));
        }

        _counterTime = counterTime;
        _clock = clock;
        _groups = groups;
    }
    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)) {
            if (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)) {
                validated = proposed;
                error = "Invalid native condition authority.";

                return false;
            }

            return WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out validated, out error);
        }

        return WiredConditionConfiguration.TryValidate(Descriptor.CanonicalName, proposed, out validated, out error);
    }

    public override bool Execute(WiredRuntimeContext context)
    {
        var config = context.ConfigurationOf(this);

        if (!TryValidateConfiguration(config, out config, out _)) {
            return false;
        }

        var negative = WiredConditionConfiguration.NegativeNames.TryGetValue(Descriptor.CanonicalName, out var positive);

        // The negative furniture-on label is the empty direction: 0 is at least one empty, 1 is all empty.
        if (negative && Descriptor.CanonicalName is "wf_cnd_not_furni_on") {
            // Observed on AIR WIN63-202610131318-860371703: the negation fails on an empty selection too, like "Furni has furni on".
            if (Furni(context, config, "items").Length == 0) {
                return false;
            }

            config = config with { IntParams = config.IntParams.SetItem(0, config.IntParams[0] == 0 ? 1 : 0) };
        }

        var result = Evaluate(positive ?? Descriptor.CanonicalName, context, config);

        return negative ? !result : result;
    }

    private bool Evaluate(string name, WiredRuntimeContext context, WiredConfiguration config)
    {
        Item[] Items() => Furni(context, config, "items");
        RoomUser[] Avatars() => Users(context, config, "users");
        bool Quantify(Func<RoomUser, bool> predicate, int index) => WiredRoomOperations.Quantify(Avatars().Select(predicate), Param(config, index));
        var now = _clock();

        switch (name) {
            case "wf_cnd_actor_dir":
                return Quantify(user => (Param(config, 0) & (1 << user.RotBody)) != 0, 2);
            case "wf_cnd_actor_in_team":
                return Quantify(user => Param(config, 0) == 0 ? user.Team != Team.None : (int)user.Team == Param(config, 0), 2);
            case "wf_cnd_has_handitem":
                return Quantify(user => user.CarryItemId == Param(config, 0), 2);
            case "wf_cnd_wearing_effect":
                return Quantify(user => !user.IsBot && user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == Param(config, 0), 2);
            case "wf_cnd_user_level":
                return Quantify(user => !user.IsBot && WiredUserLevel.Of(user) is var level
                    && (Param(config, 1) == 0 ? level < Param(config, 0) : Param(config, 1) == 1 ? level == Param(config, 0) : level > Param(config, 0)), 2);
            case "wf_cnd_wearing_badge":
                return Quantify(user => user.GetClient()?.GetHabbo()?.Inventory?.Badges.EquippedBadges.Any(badge =>
                string.Equals(badge.Code, config.Text, StringComparison.OrdinalIgnoreCase)) == true, 1);
            case "wf_cnd_actor_in_group":
                var group = context.Room.Group;

                if (Param(config, 1) == 1 && !_groups.TryGetGroup(Param(config, 2), out group)) {
                    return false;
                }

                return group != null && Quantify(user => !user.IsBot && group.IsMember(user.HabboId), 3);
            case "wf_cnd_user_performs_action":
                return Quantify(user => IsPerforming(user, config, context.Event), 6);
            case "wf_cnd_triggerer_match":
                if (config.UserSources["comparison"] == 101 && config.Text.Length == 0 && Param(config, 1) == 0) {
                    return Quantify(user => MatchesKind(user, Param(config, 0)), 4);
                }

                // Observed on AIR WIN63-202610201032-107964540: the name of any triggerer type is compared here (a bot too), the name of a
                // user with its case kept and the name of a bot without regard to case.
                var comparisons = (config.UserSources["comparison"] == 101
                        ? context.Targets.ResolveUsers(context, [], WiredSources.AllRoom)
                        : Users(context, config, "comparison"))
                    .Where(user => MatchesKind(user, Param(config, 0)))
                    .Where(user => Param(config, 1) == 0 || string.Equals(Name(user), config.Text,
                        user.IsBot ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    .Select(user => user.VirtualId).ToHashSet();

                return comparisons.Count != 0 && Quantify(user => MatchesKind(user, Param(config, 0)) && comparisons.Contains(user.VirtualId), 4);
            case "wf_cnd_trggrer_on_frn":
                return WiredItemConditions.TriggererOnItems(config, Items(), Avatars());
            case "wf_cnd_furnis_hv_avtrs":
                return WiredItemConditions.HasAvatars(config, Items(), context.Targets.AllUsers());
            case "wf_cnd_has_furni_on":
                var stackSubjects = Items();
                var ignoredSupports = Descriptor.CanonicalName == "wf_cnd_not_furni_on" && config.FurniSources["items"] == 100
                    ? stackSubjects.Select(item => item.Id).ToHashSet() : null;

                return WiredItemConditions.HasStackedFurniture(config, stackSubjects,
                    item => WiredRoomOperations.HasStackedItem(context.Room, item, ignoredSupports));
            case "wf_cnd_match_snapshot":
                return WiredItemConditions.MatchesSnapshot(config, Items());
            case "wf_cnd_stuff_is":
                return WiredItemConditions.MatchesType(config, Items(), Furni(context, config, "comparison", true));
            case "wf_cnd_has_altitude":
                return WiredItemConditions.MatchesAltitude(config, Items());
            case "wf_cnd_valid_moves":
                // Observed (AIR WIN63-202610131318-860371703): the hotel editor has no furni source. The condition checks the moves
                // of the stack's movement effects: r92k r92h-vm-fail one-w1/w2 and two-w1/w2 failed when a rel_mov step was blocked
                // by the wall (also with a second, free mover); r92g r92-valid-moves a/b/c passed when the stack's move was legal.
                var planned = context.CapturedStack.OfType<Actions.WiredModernAction>().SelectMany(action => action.PlannedMoves(context)).ToArray();
                var moving = Items();

                return planned.All(move => WiredRoomOperations.CanMoveItem(context.Room, move.Item, move.X, move.Y, move.Rotation,
                        collision: WiredRoomMovement.Collision(context.Policy.Addons.Physics, step: move.Step)))
                    && (moving.Length == 0 || WiredItemConditions.ValidMoves(moving, (item, x, y) =>
                        WiredRoomOperations.CanMoveItem(context.Room, item, x, y, item.Rotation,
                            collision: WiredRoomMovement.Collision(context.Policy.Addons.Physics, step: true))));
            case "wf_cnd_slc_quantity":
                return WiredItemConditions.SelectionQuantity(config,
                Param(config, 2) == 1 ? Items().Length : 0, Param(config, 2) == 0 ? Avatars().Length : 0);
            case "wf_cnd_user_count_in":
                var count = config.UserSources["users"] == 0 ? context.Targets.AllUsers().Count(user => !user.IsBot) : Avatars().Length;

                return count >= Param(config, 0) && count <= Param(config, 1);
            case "wf_cnd_counter_time_matches":
                var targetMs = Param(config, 1) * 60_000L + Param(config, 2) * 500L;

                // Observed on AIR WIN63-202610201032-107964540: a stored comparison of 3 passes when the counter is at most the value.
                return WiredRoomOperations.Quantify(Items().Select(item => _counterTime(item) is { } time
                    && (Param(config, 0) == 3 ? time <= targetMs : WiredRoomOperations.Compare(time, targetMs, Param(config, 0)))), Param(config, 4));
            case "wf_cnd_match_time":
                return WiredTimeConditions.MatchesTime(config, now);
            case "wf_cnd_match_date":
                return WiredTimeConditions.MatchesDate(config, now);
            case "wf_cnd_date_rng_active":
                return WiredTimeConditions.MatchesRange(config, now);
            case "wf_cnd_time_less_than":
            case "wf_cnd_time_more_than":
                return WiredTimeConditions.MatchesElapsed(config, context.Room.TimerElapsedMilliseconds(now), name == "wf_cnd_time_more_than");
            case "wf_cnd_team_has_rank":
            case "wf_cnd_team_has_score":
                var scores = context.Room.GetGameManager().Points;
                bool TeamMatches(int team) => team is >= 1 and <= 4 && (name == "wf_cnd_team_has_score"
                    ? WiredRoomOperations.Compare(scores[team], Param(config, 2), Param(config, 1))
                    : 1 + Enumerable.Range(1, 4).Count(other => scores[other] > scores[team]) == Param(config, 1) + 1);

                // "The user's team": each selected user is checked through their own team, and a user in no team fails. Observed on hhus
                // (AIR WIN63-202610131318-860371703): with the owner in a team and a bot in none, "all users" fails and "any user" passes.
                return Param(config, 0) == 0
                    ? Quantify(user => TeamMatches((int)user.Team), name == "wf_cnd_team_has_score" ? 4 : 3)
                    : TeamMatches(Param(config, 0));
            default:
                throw new InvalidOperationException("Condition has no evaluator.");
        }
    }

    public static string Name(RoomUser user) => user.IsBot ? user.BotData.Name : user.GetClient()?.GetHabbo()?.Username ?? "";
    // Polaris' editor uses pet=2/bot=4; the internal mask reverses those bits.
    public static bool MatchesKind(RoomUser user, int mask) => (mask & (user.IsPet ? 2 : user.IsBot ? 4 : 1)) != 0;
    public static bool IsPerforming(RoomUser user, WiredConfiguration config, WiredRuntimeEvent @event)
    {
        var action = Param(config, 0);

        if (@event.Kind == WiredEventKind.AvatarAction && ReferenceEquals(@event.Actor, user)
            && Triggers.WiredTriggerPredicates.MatchesAction(config, @event.Action, @event.Code)) {
            return true;
        }

        return action switch
        {
            // Observed on AIR WIN63-202610201032-107964540: Wave and Respect pass for a short time after the expression.
            1 or 5 or 12 => user.RecentExpression?.Action == action,
            4 => user.RecentExpression?.Action != (int)WiredAvatarAction.Relax,
            6 => user.HasStatus("sit"),
            7 => !user.HasStatus("sit") && !user.HasStatus("lay"),
            8 => user.HasStatus("lay"),
            // Observed on AIR WIN63-202610151745-318700621: the condition still sees a sign 3.4 s after it, long after its status is gone.
            9 => user.HeldSign is int sign && (Param(config, 1) == 0 || sign == Param(config, 2)),
            10 => user.DanceId != 0 && (Param(config, 3) == 0 || user.DanceId == Param(config, 4)),
            _ => false
        };
    }
}
