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
    public bool GiveScore(Room room, uint boxId, int playerId, Team team, int amount, int? quota, Action<WiredRuntimeEvent> publish)
    {
        if ((int)team is < 1 or > 4) {
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
    public bool Leave(Room room, RoomUser user)
    {
        if (user.IsBot || user.Team == Team.None) {
            return false;
        }

        var effect = TeamEffect(TeamType(room, user), user.Team);
        RemoveFromManagers(room, user);
        user.Team = Team.None;
        _types.Remove(user);
        user.UpdateNeeded = true;

        if (user.GetClient()?.GetHabbo()?.Effects?.CurrentEffect == effect) {
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
    // Observed on hhus (build WIN63-202610131318): a plain wired team (type 0), team 1, delivers AvatarEffect 223 to the own avatar about
    // 420 ms after the action's own stack effects, and leaving delivers effect 0 with them. 224-226 for teams 2-4 follow the
    // observed 223 and are unobserved. Battle Banzai teams wear effects 33-36 (as on the banzai tiles); Freeze teams wear 40-43.
    internal const int JoinEffectDelayMilliseconds = 420;
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
