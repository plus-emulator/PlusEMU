using System.Drawing;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// Per-item heading and nearest-avatar rules, matched to hhus (AIR WIN63-202610131318-860371703, r92d/r92f movement probes,
/// reviews/r92-movement-facts.md); the caller commits actual moves.
/// </summary>
public sealed class WiredDirectionalActions
{
    private readonly Dictionary<Item, int> _headings = [];
    // The last step each Chase took (0, 2, 4 or 6); its random step never goes straight back.
    private readonly Dictionary<Item, int> _chaseSteps = [];
    public bool MoveHeading(Item item, int initial, int turn, bool blockUsers,
        Func<int, int, bool> move, Func<int, int, RoomUser[]> usersAt, Action<Item, RoomUser> collision,
        IReadOnlySet<int>? throughUsers = null)
    {
        var heading = Heading(item, initial);
        var moved = false;

        for (var attempt = 0; attempt < 8; attempt++) {
            var offset = WiredRoomOperations.Offset(heading);
            var x = item.GetX + offset.X;
            var y = item.GetY + offset.Y;
            var occupants = usersAt(x, y);

            foreach (var user in occupants.Where(user => !user.IsBot)) {
                collision(item, user);
            }

            var blocked = blockUsers && occupants.Any(user => throughUsers?.Contains(user.VirtualId) != true);

            if (blocked) {
                break;
            }

            if (move(x, y)) {
                moved = true;
                break;
            }

            if (turn == 6) {
                break;
            }

            heading = Turn(heading, turn);
        }

        _headings[item] = heading;

        return moved;
    }
    public int Heading(Item item, int initial) => _headings.GetValueOrDefault(item, initial);
    public void Clear()
    {
        _headings.Clear();
        _chaseSteps.Clear();
    }
    public int? LastChaseStep(Item item) => _chaseSteps.TryGetValue(item, out var step) ? step : null;
    public void RememberChaseStep(Item item, int direction) => _chaseSteps[item] = direction;
    public void Retain(IEnumerable<Item> attached)
    {
        var live = attached.ToHashSet();

        foreach (var item in _headings.Keys.Where(item => !live.Contains(item)).ToArray()) {
            _headings.Remove(item);
        }

        foreach (var item in _chaseSteps.Keys.Where(item => !live.Contains(item)).ToArray()) {
            _chaseSteps.Remove(item);
        }
    }
    public static int Turn(int direction, int mode) => mode switch
    {
        1 => (direction + 2) % 8,
        2 => (direction + 6) % 8,
        3 => (direction + 1) % 8,
        4 => (direction + 7) % 8,
        5 => Random.Shared.Next(8),
        6 => direction,
        _ => (direction + 4) % 8
    };
    // Observed r92f r92-chase: from Manhattan 3 the mover steps towards the owner; from Manhattan 10 it wanders (12,1), (11,2), (10,1).
    public static RoomUser? Nearest(Item item, IEnumerable<RoomUser> users, int? range = 3) => users.Where(user => !user.IsBot)
        .Select(user => (User: user, Distance: Math.Abs(user.X - item.GetX) + Math.Abs(user.Y - item.GetY)))
        .Where(pair => range == null || pair.Distance <= range).OrderBy(pair => pair.Distance).Select(pair => pair.User).FirstOrDefault();
    public static IEnumerable<Point> Steps(Item item, RoomUser user, bool away) => Steps(item, user, away, Random.Shared);

    // Observed on AIR WIN63-202610151745-318700621 (r97_chase_tie): chasing from an exact diagonal took the y step 6 times and the x step
    // 2 times in 10 fires, so a tie between the axes is decided at random.
    public static IEnumerable<Point> Steps(Item item, RoomUser user, bool away, Random random)
    {
        var dx = user.X - item.GetX;
        var dy = user.Y - item.GetY;

        if (away) {
            dx = -dx;
            dy = -dy;
        }

        var x = new Point(item.GetX + Math.Sign(dx), item.GetY);
        var y = new Point(item.GetX, item.GetY + Math.Sign(dy));

        var xFirst = Math.Abs(dx) == Math.Abs(dy) && !away ? random.Next(2) == 0 : Math.Abs(dx) >= Math.Abs(dy);

        return (xFirst ? new[] { x, y } : [y, x])
            .Where(point => point.X != item.GetX || point.Y != item.GetY);
    }
    /// <summary>
    /// Flee: from a user in its own row or column within three tiles the block steps straight away. Otherwise, or when that tile is
    /// taken, it steps east, even towards the user. When east is taken it steps away from the nearest user on y (south when level),
    /// then the other way on y.
    /// Observed on AIR WIN63-202610151745-318700621 in a model_a room, 10 of 10 fires for each start: offsets (-3,0) and (0,-2) went
    /// straight away; (-4,0), (0,-4), every diagonal offset, and (0,2) on the south edge went east; at the east wall (3,0) and (3,1)
    /// went south and (3,-1) went north. Observed r92f r92-flee: (0,-3) against the north wall went east.
    /// </summary>
    public static IEnumerable<Point> FleeSteps(Item item, RoomUser? inLine, RoomUser nearest)
    {
        if (inLine != null) {
            yield return new(item.GetX + Math.Sign(item.GetX - inLine.X), item.GetY + Math.Sign(item.GetY - inLine.Y));
        }

        yield return new(item.GetX + 1, item.GetY);

        var south = item.GetY >= nearest.Y ? 1 : -1;

        yield return new(item.GetX, item.GetY + south);
        yield return new(item.GetX, item.GetY - south);
    }
    public static int AvatarRotation(int body, int raw) => raw switch
    { 8 => (body + 1) % 8, 9 => (body + 7) % 8, _ => raw };
}
