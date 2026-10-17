using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Room-scoped typed game grants, separate from the legacy editor's arithmetic operation.</summary>
public sealed class WiredGameState
{
    private readonly Dictionary<(uint Box, int Player), int> _grants = [];
    private readonly Dictionary<RoomUser, (int Type, Team Team)> _types = [];
    private static readonly ConditionalWeakTable<Room, WiredGameState> Rooms = new();
    public static WiredGameState For(Room room) => Rooms.GetValue(room, _ => new());
    public void ResetQuotas() => _grants.Clear();
    public void Forget(RoomUser user) => _types.Remove(user);
    public void Clear()
    {
        _grants.Clear();
        _types.Clear();
    }
    /// <summary>A Battle Banzai, Freeze or football game, or a Wired game counter, is in progress: the only time a team score exists.</summary>
    public static bool GameRunning(Room room) => room.GetBanzai().IsBanzaiActive || room.GetFreeze().GameIsStarted || room.GetSoccer().GameIsStarted
        || room.GetWired() is { GameCounterRunning: true };
    // Observed on hhus (build WIN63-202610131318), room without a running game: Give Score (team 0, 1 point) and Give Score To Team
    // (team 1, 1 point) both flashed their boxes, but the Score Achieved stacks (threshold 1, any team and team 1) never fired and the
    // hotel sent no score related packet. Outside a running game giving score is a no-op: no points, no quota use, no score event.
    public bool GiveScore(Room room, uint boxId, int playerId, Team team, int amount, int? quota, Action<WiredRuntimeEvent> publish)
    {
        if ((int)team is < 1 or > 4 || !GameRunning(room)) {
            return false;
        }

        var key = (boxId, playerId);

        if (quota is > 0) {
            var grants = _grants.GetValueOrDefault(key);

            if (grants >= quota) {
                return false;
            }

            _grants[key] = grants + 1;
        }

        var previous = room.GetGameManager().Points[(int)team];
        room.GetGameManager().AddPointToTeam(team, amount);
        publish(new(WiredEventKind.Score) { Team = (int)team, PreviousValue = previous, Value = room.GetGameManager().Points[(int)team] });

        return true;
    }
    public bool Join(Room room, RoomUser user, int type, Team chosen, int mode, IEnumerable<RoomUser> allUsers,
        Action<RoomUser, Team, int>? applyEffect = null)
    {
        if (user.IsBot) {
            return false;
        }

        var manager = type switch { 1 => room.GetTeamManagerForBanzai(), 2 => room.GetTeamManagerForFreeze(), _ => null };
        var candidates = Enumerable.Range(1, 4).Select(value => (Team)value).ToArray();
        var members = allUsers.Where(member => !member.IsBot && !ReferenceEquals(member, user) && TeamType(room, member) == type).ToArray();
        var team = mode switch
        {
            1 => candidates.OrderBy(team => members.Count(member => member.Team == team)).ThenBy(team => user.Team == team ? 0 : 1).First(),
            2 => candidates[Random.Shared.Next(candidates.Length)],
            _ => chosen
        };

        if ((int)team is < 1 or > 4 || manager != null && user.Team != team && !manager.CanEnterOnTeam(team)) {
            return false;
        }

        if (user.Team == team && TeamType(room, user) == type) {
            return false;
        }

        RemoveFromManagers(room, user);
        user.Team = team;
        _types[user] = (type, team);
        manager?.AddUser(user);

        if (applyEffect == null) {
            user.ApplyEffect(TeamEffect(type, team));
        }
        else {
            applyEffect(user, team, TeamEffect(type, team));
        }

        user.UpdateNeeded = true;

        return true;
    }
    public bool Leave(Room room, RoomUser user, Action<RoomUser, int>? clearEffect = null)
    {
        if (user.IsBot || user.Team == Team.None) {
            return false;
        }

        var effect = TeamEffect(TeamType(room, user), user.Team);
        RemoveFromManagers(room, user);
        user.Team = Team.None;
        _types.Remove(user);
        user.UpdateNeeded = true;

        if (clearEffect != null) {
            clearEffect(user, effect);
        }
        else if (user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == effect) {
            user.ApplyEffect(0);
        }

        return true;
    }
    public int? ReadTeamType(Room room, RoomUser user)
    {
        if (user.IsBot || user.Team == Team.None) {
            return null;
        }

        if (Contains(room.Teambanzai, user)) {
            return 0;
        }

        if (Contains(room.Teamfreeze, user)) {
            return 1;
        }

        return _types.TryGetValue(user, out var joined) && joined.Team == user.Team && joined.Type == 0 ? 4 : null;
    }
    // Observed on hhus (AIR WIN63-202610131318-860371703): a user in a plain Wired team talks, shouts and whispers with the team bubble,
    // 130 in Red and 133 in Green, and 0 outside a team. Blue 131 and Yellow 132 follow the @event.chat.style connector's team styles.
    public static int? TeamChatStyle(Room room, RoomUser user) => For(room).ReadTeamType(room, user) == 4
        ? user.Team switch { Team.Red => 130, Team.Blue => 131, Team.Yellow => 132, Team.Green => 133, _ => null }
        : null;
    // Observed on hhus (build WIN63-202610131318): the effect is 222 + team for a plain wired team (type 0, team 1 gave 223), 32 + team for
    // a Battle Banzai team (team 2 gave 34) and 39 + team for a Freeze team (team 3 gave 42); leaving delivers effect 0. 224-226 for
    // plain teams 2-4 follow the observed 223 and are unobserved. The own avatar gets it 54 to 498 ms after the action's own stack effects
    // (join 54, 273, 420; leave 306, 498), so it rides the next 500 ms state update instead of a fixed delay.
    internal static int TeamEffect(int type, Team team) => (int)team + type switch { 1 => 32, 2 => 39, _ => 222 };
    private int TeamType(Room room, RoomUser user) => _types.TryGetValue(user, out var joined) && joined.Team == user.Team ? joined.Type
        : Contains(room.Teambanzai, user) ? 1 : Contains(room.Teamfreeze, user) ? 2 : 0;
    private static bool Contains(TeamManager? manager, RoomUser user) => manager != null
        && (manager.RedTeam.Contains(user) || manager.GreenTeam.Contains(user) || manager.BlueTeam.Contains(user) || manager.YellowTeam.Contains(user));
    private static void RemoveFromManagers(Room room, RoomUser user)
    {
        if (Contains(room.Teambanzai, user)) {
            room.Teambanzai.OnUserLeave(user);
        }

        if (Contains(room.Teamfreeze, user)) {
            room.Teamfreeze.OnUserLeave(user);
        }
    }
}
