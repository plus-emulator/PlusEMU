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
        if (t.Equals(Team.Blue)) {
            return BlueTeam.Count < 5;
        }

        if (t.Equals(Team.Red)) {
            return RedTeam.Count < 5;
        }

        if (t.Equals(Team.Yellow)) {
            return YellowTeam.Count < 5;
        }

        if (t.Equals(Team.Green)) {
            return GreenTeam.Count < 5;
        }

        return false;
    }

    public void AddUser(RoomUser user)
    {
        if (user.Team.Equals(Team.Blue) && !BlueTeam.Contains(user)) {
            BlueTeam.Add(user);
        }
        else if (user.Team.Equals(Team.Red) && !RedTeam.Contains(user)) {
            RedTeam.Add(user);
        }
        else if (user.Team.Equals(Team.Yellow) && !YellowTeam.Contains(user)) {
            YellowTeam.Add(user);
        }
        else if (user.Team.Equals(Team.Green) && !GreenTeam.Contains(user)) {
            GreenTeam.Add(user);
        }

        if (user.GetClient()?.GetHabbo()?.CurrentRoom is not { } room) {
            return;
        }

        ShowGateCounts(room, user.Team);

        // A full Banzai team locks its Banzai gate.
        if (Game.Equals("banzai", StringComparison.OrdinalIgnoreCase) && Members(user.Team).Count == 5) {
            foreach (var gate in BanzaiGates(room, user.Team)) {
                room.GetGameMap().SetFloorStatus(gate.GetX, gate.GetY, 0);
            }
        }
    }

    public void OnUserLeave(RoomUser user)
    {
        var team = user.Team;

        if (Members(team).Contains(user)) {
            Members(team).Remove(user);
        }

        if (user.GetClient()?.GetHabbo()?.CurrentRoom is not { } room) {
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

    public bool Has(RoomUser user) => Members(user.Team).Contains(user);

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
    /// Observed on AIR WIN63-202610201032-107964540: a Banzai gate and a Freeze gate of one colour are one team. Both show the number of
    /// players in it and change in one ObjectsDataUpdate when a player joins or leaves.
    /// </summary>
    private static void ShowGateCounts(Room room, Team team)
    {
        var players = room.GetTeamManagerForBanzai().Members(team).Concat(room.GetTeamManagerForFreeze().Members(team)).Distinct().Count();
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
