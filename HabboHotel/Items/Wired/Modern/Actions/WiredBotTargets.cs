using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Actual target identities for bot movement/following and one arrival per approach.</summary>
public sealed class WiredBotTargets
{
    private sealed class Target(Item? item, RoomUser? user)
    {
        public Item? Item = item; public RoomUser? User = user; public bool Reached;
        // A bot carrying a hand item to User (Bot Give Handitem), and when its step next to the user was announced.
        public int HandItem; public long? AnnouncedAt; public (int X, int Y)? Goal; public (int X, int Y)? LeftFrom;
    }
    /// <summary>
    /// Observed on AIR WIN63-202610151745-318700621: the user gets the item 300 ms after the bot's step next to them is announced.
    /// </summary>
    public const int HandOverDelayMilliseconds = 300;
    private readonly Dictionary<RoomUser, Target> _targets = [];
    private static readonly ConditionalWeakTable<Room, WiredBotTargets> Rooms = new();
    public static WiredBotTargets For(Room room) => Rooms.GetValue(room, _ => new());
    public bool HasTargets => _targets.Count != 0;
    public void Walk(RoomUser bot, Item item) => _targets[bot] = new(item, null);
    // A bot told to follow a user it already stands next to has not reached anyone yet. Observed on AIR WIN63-202610151745-318700621
    // (wf_trg_bot_reached_avtr.bot): with the bot adjacent to the owner, Follow Avatar fired no Bot Reached Avatar.
    public void Follow(RoomUser bot, RoomUser user) => _targets[bot] = new(null, user)
    {
        Reached = Math.Max(Math.Abs(user.X - bot.X), Math.Abs(user.Y - bot.Y)) <= 1
    };
    /// <summary>
    /// Bot Give Handitem. Observed on AIR WIN63-202610151745-318700621: the bot takes the item at once; standing next to the user
    /// (diagonals count) it hands it over in the same instant, otherwise it walks to the user's nearest free neighbouring tile first.
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
            // Observed on AIR WIN63-202610151745-318700621: a bot carrying a hand item to a user who left keeps walking to where they
            // stood and lets the item go there.
            if (target.HandItem != 0) {
                target.User = null;
                target.LeftFrom = (user.X, user.Y);
                continue;
            }

            Stop(bot);
        }
    }
    public void Clear() => _targets.Clear();
    public void Stop(RoomUser bot)
    {
        _targets.Remove(bot);

        // Only bots are ever tracked here; their forced movement is cleared before the walk stops.
        if (bot.BotData is { } botData) {
            botData.ForcedMovement = false;
            botData.ForcedUserTargetMovement = 0;
        }

        bot.ClearMovement(true);
    }
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

            if (target.Item is { } item) {
                if (!ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
                    Stop(bot);
                    continue;
                }

                if (!WiredRoomOperations.IsOnItem(bot, item)) {
                    continue;
                }

                result.Add(new(WiredEventKind.BotReachedFurni) { Actor = bot, EventItem = item });
                Stop(bot);
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

                var near = Math.Max(Math.Abs(user.X - bot.X), Math.Abs(user.Y - bot.Y)) <= 1;

                if (near && !target.Reached) {
                    result.Add(new(WiredEventKind.BotReachedUser) { Actor = bot, TargetUser = user });
                }

                target.Reached = near;

                if (near) {
                    continue;
                }

                var destinations = Enumerable.Range(0, 4).Select(direction => WiredRoomOperations.Offset(direction * 2))
                    .Select(offset => (X: user.X + offset.X, Y: user.Y + offset.Y))
                    .Where(point => room.GetGameMap().ValidTile(point.X, point.Y) && room.GetGameMap().CanWalk(point.X, point.Y, false))
                    .OrderBy(point => Math.Abs(point.X - bot.X) + Math.Abs(point.Y - bot.Y)).ToArray();

                if (destinations.Length != 0) {
                    bot.MoveTo(destinations[0].X, destinations[0].Y);
                }
            }
        }

        return result;
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
            // Observed on AIR WIN63-202610151745-318700621: a bot that cannot get next to the spot (the user stood boxed in) keeps
            // holding the item.
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

    // Observed on AIR WIN63-202610151745-318700621: the bot ends on the free tile around the user closest to it (diagonals count),
    // nearer in straight-line distance on a tie.
    private static void Approach(Room room, RoomUser bot, RoomUser user, Target target)
    {
        var map = room.GetGameMap();
        var goal = Enumerable.Range(0, 8).Select(WiredRoomOperations.Offset)
            .Select(offset => (X: user.X + offset.X, Y: user.Y + offset.Y))
            .Where(point => map.ValidTile(point.X, point.Y) && map.CanWalk(point.X, point.Y, false))
            .OrderBy(point => Math.Max(Math.Abs(point.X - bot.X), Math.Abs(point.Y - bot.Y)))
            .ThenBy(point => (point.X - bot.X) * (point.X - bot.X) + (point.Y - bot.Y) * (point.Y - bot.Y))
            .Select(point => ((int X, int Y)?)point).FirstOrDefault();

        if (goal is { } tile && target.Goal != tile) {
            target.Goal = tile;
            bot.MoveTo(tile.X, tile.Y);
        }
    }
}
