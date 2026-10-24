using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Actual target identities for bot movement/following and one arrival per approach.</summary>
public sealed class WiredBotTargets
{
    private sealed class Target(Item? item, RoomUser? user)
    {
        public Item? Item = item; public RoomUser? User = user;
        // A bot carrying a hand item to User (Bot Give Handitem), and when its step next to the user was announced.
        public int HandItem; public long? AnnouncedAt; public (int X, int Y)? Goal; public (int X, int Y)? LeftFrom;
        // A following bot rests from this time on the room's Wired clock; Arrived when the rest follows a stop on its tile.
        public long? RestFrom; public bool Arrived;
        // The account a following bot is tied to. User is null while that account is out of the room.
        public int FollowId;
    }
    /// <summary>The user gets the item 300 ms after the bot's step next to them is announced.</summary>
    public const int HandOverDelayMilliseconds = 300;
    /// <summary>A following bot that reached its tile waits one room tick before it looks for the user again.</summary>
    public const int FollowRestMilliseconds = 500;
    // Picks the tile around the user that a following bot walks to.
    internal Random Random { get; set; } = Random.Shared;
    private readonly Dictionary<RoomUser, Target> _targets = [];
    private static readonly ConditionalWeakTable<Room, WiredBotTargets> Rooms = new();
    public static WiredBotTargets For(Room room) => Rooms.GetValue(room, _ => new());
    public bool HasTargets => _targets.Count != 0;
    public void Walk(RoomUser bot, Item item) => _targets[bot] = new(item, null);
    public void Follow(RoomUser bot, RoomUser user) => _targets[bot] = new(null, user) { FollowId = user.IsBot ? 0 : user.HabboId };
    /// <summary>
    /// Bot Give Handitem. The bot takes the item at once. Next to the user (diagonals count) it hands the item over in the same instant,
    /// otherwise it first walks to the user's nearest free neighbouring tile.
    /// </summary>
    public void Give(Room room, RoomUser bot, RoomUser user, int handItem, long now)
    {
        bot.CarryItem(handItem);

        if (Near(WiredRoomOperations.StandingTile(bot), user)) {
            HandOver(bot, user, handItem);

            return;
        }

        var target = new Target(null, user) { HandItem = handItem };
        _targets[bot] = target;
        Approach(room, bot, user, target);
    }
    /// <summary>When the step of a bot carrying a hand item next to its user was announced, on the room's Wired clock.</summary>
    internal long? HandOverAnnouncedAt(RoomUser bot) => _targets.GetValueOrDefault(bot)?.AnnouncedAt;
    public void Forget(RoomUser user)
    {
        _targets.Remove(user);

        foreach (var (bot, target) in _targets.Where(entry => ReferenceEquals(entry.Value.User, user)).ToArray()) {
            // A bot carrying a hand item to a user who left keeps walking to where they stood and lets the item go there.
            if (target.HandItem != 0) {
                target.User = null;
                target.LeftFrom = (user.X, user.Y);
                continue;
            }

            if (target.FollowId != 0) {
                target.User = null;
                continue;
            }

            Stop(bot);
        }
    }
    public void Clear() => _targets.Clear();
    // A bot that stops following finishes the walk it is on. Any other stop ends the walk.
    public void Stop(RoomUser bot)
    {
        var following = _targets.GetValueOrDefault(bot) is { FollowId: not 0 };
        _targets.Remove(bot);

        // Only bots are ever tracked here; their forced movement is cleared before the walk stops.
        if (bot.BotData is { } botData) {
            botData.ForcedMovement = false;
            botData.ForcedUserTargetMovement = 0;
        }

        // A bot that arrived, or that already stands still, sends no extra status.
        if (!following && IsMoving(bot)) {
            bot.ClearMovement(true);
        }
    }

    /// <summary>Whether the bot stands still on the tile, with no walk or step under way and none queued.</summary>
    internal static bool StandsStillOn(RoomUser bot, int x, int y) => bot.X == x && bot.Y == y && !IsMoving(bot);

    private static bool IsMoving(RoomUser bot) => bot.IsWalking || bot.SetStep || bot.Movement.HasIntent
        || bot.Movement.Commands.Read()?.Sequence > bot.Movement.ConsumedSequence;
    public IReadOnlyList<WiredRuntimeEvent> Poll(Room room) => Poll(room, Environment.TickCount64);
    public IReadOnlyList<WiredRuntimeEvent> Poll(Room room, long now)
    {
        var result = new List<WiredRuntimeEvent>();
        var attached = room.GetRoomUserManager().GetUserList().ToHashSet();

        foreach (var (bot, target) in _targets.ToArray()) {
            if (!attached.Contains(bot)) {
                _targets.Remove(bot);
                continue;
            }

            if (target.FollowId != 0 && (target.User is null || !attached.Contains(target.User))) {
                target.User = attached.FirstOrDefault(other => !other.IsBot && other.HabboId == target.FollowId);

                if (target.User is null) {
                    continue;
                }
            }

            if (target.Item is { } item) {
                if (!ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
                    Stop(bot);
                    continue;
                }

                // Bot Reaches Furni was raised when the step onto the furni was announced.
                if (WiredRoomOperations.IsOnItem(bot, item)) {
                    Stop(bot);
                }
            }
            else if (target is { HandItem: not 0, LeftFrom: { } left }) {
                PollDeparted(bot, left, target, now);
            }
            else if (target.User is { } user) {
                if (!attached.Contains(user)) {
                    Stop(bot);
                    continue;
                }

                if (target.HandItem != 0) {
                    PollGive(room, bot, user, target, now);
                    continue;
                }

                if (PollFollow(room, bot, user, target, now)) {
                    result.Add(new(WiredEventKind.BotReachedUser) { Actor = bot, TargetUser = user });
                }
            }
        }

        return result;
    }

    // A following bot picks a random free tile among the 8 around the tile the user stands on or steps onto.
    // It walks all the way there and does not move when it is already next to the user.
    // Bot Reached Avatar fires one room tick after the bot stops on its tile next to the user, and only then.
    // Returns whether the bot reached the user.
    private bool PollFollow(Room room, RoomUser bot, RoomUser user, Target target, long now)
    {
        if (target.Goal is { } goal) {
            var arrived = !bot.SetStep && bot.X == goal.X && bot.Y == goal.Y;

            if (!arrived && IsMoving(bot)) {
                return false;
            }

            target.Goal = null;
            target.Arrived = arrived;
            target.RestFrom = now;
        }

        if (target.RestFrom is { } rest && now - rest < FollowRestMilliseconds) {
            return false;
        }

        var arrival = target.Arrived;
        target.Arrived = false;
        var tile = WiredRoomOperations.StandingTile(user);

        if (Math.Max(Math.Abs(tile.X - bot.X), Math.Abs(tile.Y - bot.Y)) <= 1) {
            return arrival;
        }

        var map = room.GetGameMap();
        var free = Enumerable.Range(0, 8).Select(Rotation.Offset).Select(offset => (X: tile.X + offset.X, Y: tile.Y + offset.Y))
            .Where(point => map.ValidTile(point.X, point.Y) && map.SquareIsOpen(point.X, point.Y, false)
                && WiredRoomOperations.UsersStandingOn(room, new(point.X, point.Y)).All(other => ReferenceEquals(other, bot)))
            .ToArray();

        if (free.Length == 0) {
            target.RestFrom = now;

            return false;
        }

        var pick = free[Random.Next(free.Length)];
        target.Goal = pick;
        bot.MoveTo(pick.X, pick.Y);

        return false;
    }

    private void PollGive(Room room, RoomUser bot, RoomUser user, Target target, long now)
    {
        var step = WiredRoomOperations.StandingTile(bot);

        if (!Near(step, user)) {
            target.AnnouncedAt = null;
            Approach(room, bot, user, target);

            return;
        }

        if (target.AnnouncedAt is not { } announced) {
            target.AnnouncedAt = now;

            // Finish the step next to the user and stop there.
            if (target.Goal != (step.X, step.Y)) {
                target.Goal = (step.X, step.Y);
                bot.MoveTo(step.X, step.Y);
            }

            return;
        }

        if (now - announced >= HandOverDelayMilliseconds) {
            _targets.Remove(bot);
            HandOver(bot, user, target.HandItem);
        }
    }

    private void PollDeparted(RoomUser bot, (int X, int Y) left, Target target, long now)
    {
        var step = WiredRoomOperations.StandingTile(bot);

        if (Math.Max(Math.Abs(left.X - step.X), Math.Abs(left.Y - step.Y)) > 1) {
            // A bot that cannot get next to the spot (the user stood boxed in) keeps holding the item.
            if (!bot.IsWalking && !bot.SetStep) {
                _targets.Remove(bot);
            }

            return;
        }

        target.AnnouncedAt ??= now;

        if (now - target.AnnouncedAt >= HandOverDelayMilliseconds) {
            _targets.Remove(bot);
            bot.CarryItem(0);
        }
    }

    private static void HandOver(RoomUser bot, RoomUser user, int handItem)
    {
        user.CarryItem(handItem);
        bot.CarryItem(0);
    }

    private static bool Near(System.Drawing.Point tile, RoomUser user) => Math.Max(Math.Abs(user.X - tile.X), Math.Abs(user.Y - tile.Y)) <= 1;

    // The bot ends on the free tile around the user closest to it (diagonals count), nearer in straight-line distance on a tie.
    // A tile the bot cannot enter is never the goal.
    private static void Approach(Room room, RoomUser bot, RoomUser user, Target target)
    {
        var map = room.GetGameMap();
        var goal = Enumerable.Range(0, 8).Select(Rotation.Offset)
            .Select(offset => (X: user.X + offset.X, Y: user.Y + offset.Y))
            .Where(point => map.ValidTile(point.X, point.Y) && map.SquareIsOpen(point.X, point.Y, false) && map.CanWalk(point.X, point.Y, false))
            .OrderBy(point => Math.Max(Math.Abs(point.X - bot.X), Math.Abs(point.Y - bot.Y)))
            .ThenBy(point => (point.X - bot.X) * (point.X - bot.X) + (point.Y - bot.Y) * (point.Y - bot.Y))
            .Select(point => ((int X, int Y)?)point).FirstOrDefault();

        if (goal is { } tile && target.Goal != tile) {
            target.Goal = tile;
            bot.MoveTo(tile.X, tile.Y);
        }
    }
}
