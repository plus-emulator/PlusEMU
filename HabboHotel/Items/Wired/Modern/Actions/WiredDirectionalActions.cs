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
    public void Clear() => _headings.Clear();
    public void Retain(IEnumerable<Item> attached)
    {
        var live = attached.ToHashSet();

        foreach (var item in _headings.Keys.Where(item => !live.Contains(item)).ToArray()) {
            _headings.Remove(item);
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
    public static IEnumerable<Point> Steps(Item item, RoomUser user, bool away)
    {
        var dx = user.X - item.GetX;
        var dy = user.Y - item.GetY;

        if (away) {
            dx = -dx;
            dy = -dy;
        }

        var x = new Point(item.GetX + Math.Sign(dx), item.GetY);
        var y = new Point(item.GetX, item.GetY + Math.Sign(dy));

        return (Math.Abs(dx) >= Math.Abs(dy) ? new[] { x, y } : [y, x])
            .Where(point => point.X != item.GetX || point.Y != item.GetY);
    }
    /// <summary>
    /// Flee: away along the larger axis, then the other axis; when neither leads away (a wall behind), a sidestep to the
    /// mover's right of the away heading, then its left.
    /// Observed r92f r92-flee: (4,2) with the owner on (4,4) moves to (4,1); (4,1) against the north wall moves to (5,1) on both fires.
    /// Observed r92j r92h-flee-sides: from (8,1) with the owner straight south on (8,3) and both sides free, the sidestep went east
    /// (the right of the away heading) on all six trials.
    /// </summary>
    public static IEnumerable<Point> FleeSteps(Item item, RoomUser user)
    {
        foreach (var step in Steps(item, user, true)) {
            yield return step;
        }

        var dx = item.GetX - user.X;
        var dy = item.GetY - user.Y;
        var away = Math.Abs(dx) >= Math.Abs(dy) ? (dx >= 0 ? 2 : 6) : (dy >= 0 ? 4 : 0);

        foreach (var side in new[] { (away + 2) % 8, (away + 6) % 8 }) {
            var offset = WiredRoomOperations.Offset(side);

            yield return new(item.GetX + offset.X, item.GetY + offset.Y);
        }
    }
    public static int AvatarRotation(int body, int raw) => raw switch
    { 8 => (body + 1) % 8, 9 => (body + 7) % 8, _ => raw };
}
