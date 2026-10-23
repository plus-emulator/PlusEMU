using Plus.HabboHotel.Items;

namespace Plus.HabboHotel.Rooms.Games.Teams;

public class TeamManager
{
    public List<RoomUser> BlueTeam;
    public string Game;
    public List<RoomUser> GreenTeam;
    public List<RoomUser> RedTeam;
    public List<RoomUser> YellowTeam;

    public static TeamManager CreateTeam(string game)
    {
        var t = new TeamManager();
        t.Game = game;
        t.BlueTeam = new();
        t.RedTeam = new();
        t.GreenTeam = new();
        t.YellowTeam = new();

        return t;
    }

    public bool CanEnterOnTeam(Team t)
    {
        return t is Team.Blue or Team.Red or Team.Yellow or Team.Green && Count(Members(t)) < 5;
    }

    public void AddUser(RoomUser user)
    {
        var members = Members(user.Team);

        lock (members) {
            if (!members.Contains(user)) {
                members.Add(user);
            }
        }

        if (RoomOf(user) is not { } room) {
            return;
        }

        ShowGateCounts(room, user.Team);

        // A full Banzai team locks its Banzai gate.
        if (Game.Equals("banzai", StringComparison.OrdinalIgnoreCase) && Count(members) == 5) {
            foreach (var gate in BanzaiGates(room, user.Team)) {
                room.GetGameMap().SetFloorStatus(gate.GetX, gate.GetY, 0);
            }
        }
    }

    public void OnUserLeave(RoomUser user)
    {
        var team = user.Team;

        var members = Members(team);

        lock (members) {
            members.Remove(user);
        }

        // The session has already left the room when this runs on leaving, so the room comes from the user.
        if (RoomOf(user) is not { } room) {
            return;
        }

        ShowGateCounts(room, team);

        if (Game.Equals("banzai", StringComparison.OrdinalIgnoreCase)) {
            foreach (var gate in BanzaiGates(room, team)) {
                if (room.GetGameMap().GameMap[gate.GetX, gate.GetY] == 0) {
                    room.GetGameMap().SetFloorStatus(gate.GetX, gate.GetY, 1);
                }
            }
        }
    }

    public bool Has(RoomUser user) => Contains(Members(user.Team), user);

    private static Room? RoomOf(RoomUser user) => user.AttachedRoom ?? user.GetClient()?.GetHabbo()?.CurrentRoom;

    private static int Count(List<RoomUser> members)
    {
        lock (members) {
            return members.Count;
        }
    }

    private static bool Contains(List<RoomUser> members, RoomUser user)
    {
        lock (members) {
            return members.Contains(user);
        }
    }

    private static RoomUser[] Snapshot(List<RoomUser> members)
    {
        lock (members) {
            return [.. members];
        }
    }

    private List<RoomUser> Members(Team team) => team switch
    {
        Team.Blue => BlueTeam,
        Team.Red => RedTeam,
        Team.Yellow => YellowTeam,
        Team.Green => GreenTeam,
        _ => []
    };

    private static IEnumerable<Item> BanzaiGates(Room room, Team team) => room.GetGameManager().Gates(team).Where(gate =>
        gate.Definition.InteractionType is InteractionType.Banzaigateblue or InteractionType.Banzaigatered or InteractionType.Banzaigategreen
            or InteractionType.Banzaigateyellow);

    /// <summary>
    /// A Banzai gate and a Freeze gate of one colour are one team. Both show the number of players in it and change in one ObjectsDataUpdate when a player joins or leaves.
    /// </summary>
    private static void ShowGateCounts(Room room, Team team)
    {
        var players = Snapshot(room.GetTeamManagerForBanzai().Members(team)).Concat(Snapshot(room.GetTeamManagerForFreeze().Members(team))).Distinct().Count();
        var gates = room.GetGameManager().Gates(team);

        foreach (var gate in gates) {
            gate.LegacyDataString = players.ToString();
            gate.UpdateState(true, false);
        }

        room.SendObjectsData(gates);
    }

    public void Dispose()
    {
        BlueTeam.Clear();
        GreenTeam.Clear();
        RedTeam.Clear();
        YellowTeam.Clear();
    }
}
