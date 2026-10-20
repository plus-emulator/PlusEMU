using System.Collections.Concurrent;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Rooms.Games;

public class GameManager
{
    private ConcurrentDictionary<uint, Item> _blueTeamItems;
    private ConcurrentDictionary<uint, Item> _greenTeamItems;
    private ConcurrentDictionary<uint, Item> _redTeamItems;
    private ConcurrentDictionary<uint, Item> _yellowTeamItems;
    private Room _room;
    private readonly TimeProvider _clock;

    public GameManager(Room room, TimeProvider clock)
    {
        _room = room;
        _clock = clock;
        Highscores = new(room, clock);
        Points = new int[5];
        _redTeamItems = new();
        _blueTeamItems = new();
        _greenTeamItems = new();
        _yellowTeamItems = new();
    }

    internal RoomHighscores Highscores { get; }

    public int[] Points { get; set; }

    public Team GetWinningTeam()
    {
        var winning = 1;
        var highestScore = 0;

        for (var i = 1; i < 5; i++) {
            if (Points[i] > highestScore) {
                highestScore = Points[i];
                winning = i;
            }
        }

        return (Team)winning;
    }

    public void AddPointToTeam(Team team, int points)
    {
        var newPoints = Points[Convert.ToInt32(team)] += points;

        if (newPoints < 0) {
            newPoints = 0;
        }

        Points[Convert.ToInt32(team)] = newPoints;
        PublishScores(ScoreCounters(team));
    }

    public void Reset()
    {
        Highscores.Invalidate();
        ZeroScores();
    }

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: a Banzai or Freeze game start sets every team score and every Banzai and
    /// Freeze score counter to 0 in one update. The scores stay after the game ends.
    /// </summary>
    internal void ZeroScores()
    {
        var counters = new List<Item>();

        for (var team = 1; team < Points.Length; team++) {
            Points[team] = 0;
            counters.AddRange(ScoreCounters((Team)team));
        }

        PublishScores(counters);
    }

    // Observed on AIR WIN63-202610201032-107964540: the Banzai and Freeze score counters of one colour show the same number, and
    // football goals and counters never change in a Banzai or Freeze game.
    private List<Item> ScoreCounters(Team team) =>
        [.. GetFurniItems(team).Values.Where(item => IsScoreCounter(item.Definition.InteractionType))];

    private void PublishScores(List<Item> counters)
    {
        foreach (var item in counters) {
            item.LegacyDataString = Points[Convert.ToInt32(item.Team)].ToString();
            item.UpdateState(true, false);
        }

        _room.SendObjectsData(counters);
    }

    internal List<Item> Gates(Team team) => [.. GetFurniItems(team).Values.Where(item => IsTeamGate(item.Definition.InteractionType))];

    private int GetScoreForTeam(Team team) => Points[Convert.ToInt32(team)];

    private ConcurrentDictionary<uint, Item> GetFurniItems(Team team)
    {
        switch (team) {
            default:
                return new();
            case Team.Blue:
                return _blueTeamItems;
            case Team.Green:
                return _greenTeamItems;
            case Team.Red:
                return _redTeamItems;
            case Team.Yellow:
                return _yellowTeamItems;
        }
    }

    private static bool IsScoreCounter(InteractionType type) => type is InteractionType.Banzaiscoreblue or InteractionType.Banzaiscoregreen
        or InteractionType.Banzaiscorered or InteractionType.Banzaiscoreyellow or InteractionType.Freezebluecounter
        or InteractionType.Freezegreencounter or InteractionType.Freezeredcounter or InteractionType.Freezeyellowcounter;

    internal static bool IsTeamGate(InteractionType type) => type is InteractionType.FreezeBlueGate or InteractionType.FreezeGreenGate
        or InteractionType.FreezeRedGate or InteractionType.FreezeYellowGate or InteractionType.Banzaigateblue or InteractionType.Banzaigatered
        or InteractionType.Banzaigategreen or InteractionType.Banzaigateyellow;

    public static Team TeamOf(InteractionType type) => type switch
    {
        InteractionType.FootballGoalRed or InteractionType.Footballcounterred or InteractionType.Banzaiscorered or InteractionType.Banzaigatered
            or InteractionType.Freezeredcounter or InteractionType.FreezeRedGate => Team.Red,
        InteractionType.FootballGoalGreen or InteractionType.Footballcountergreen or InteractionType.Banzaiscoregreen or InteractionType.Banzaigategreen
            or InteractionType.Freezegreencounter or InteractionType.FreezeGreenGate => Team.Green,
        InteractionType.FootballGoalBlue or InteractionType.Footballcounterblue or InteractionType.Banzaiscoreblue or InteractionType.Banzaigateblue
            or InteractionType.Freezebluecounter or InteractionType.FreezeBlueGate => Team.Blue,
        InteractionType.FootballGoalYellow or InteractionType.Footballcounteryellow or InteractionType.Banzaiscoreyellow or InteractionType.Banzaigateyellow
            or InteractionType.Freezeyellowcounter or InteractionType.FreezeYellowGate => Team.Yellow,
        _ => Team.None
    };

    public void AddFurnitureToTeam(Item item, Team team)
    {
        item.Team = team;

        switch (team) {
            case Team.Blue:
                _blueTeamItems.TryAdd(item.Id, item);
                break;
            case Team.Green:
                _greenTeamItems.TryAdd(item.Id, item);
                break;
            case Team.Red:
                _redTeamItems.TryAdd(item.Id, item);
                break;
            case Team.Yellow:
                _yellowTeamItems.TryAdd(item.Id, item);
                break;
        }
    }

    public void RemoveFurnitureFromTeam(Item item, Team team)
    {
        switch (team) {
            case Team.Blue:
                _blueTeamItems.TryRemove(item.Id, out item);
                break;
            case Team.Green:
                _greenTeamItems.TryRemove(item.Id, out item);
                break;
            case Team.Red:
                _redTeamItems.TryRemove(item.Id, out item);
                break;
            case Team.Yellow:
                _yellowTeamItems.TryRemove(item.Id, out item);
                break;
        }
    }

    public void LockGates()
    {
        foreach (var item in _redTeamItems.Values.ToList()) {
            LockGate(item);
        }

        foreach (var item in _greenTeamItems.Values.ToList()) {
            LockGate(item);
        }

        foreach (var item in _blueTeamItems.Values.ToList()) {
            LockGate(item);
        }

        foreach (var item in _yellowTeamItems.Values.ToList()) {
            LockGate(item);
        }
    }

    public void UnlockGates()
    {
        foreach (var item in _redTeamItems.Values.ToList()) {
            UnlockGate(item);
        }

        foreach (var item in _greenTeamItems.Values.ToList()) {
            UnlockGate(item);
        }

        foreach (var item in _blueTeamItems.Values.ToList()) {
            UnlockGate(item);
        }

        foreach (var item in _yellowTeamItems.Values.ToList()) {
            UnlockGate(item);
        }
    }

    private void LockGate(Item item)
    {
        if (IsTeamGate(item.Definition.InteractionType)) {
            _room.GetGameMap().SetFloorStatus(item.GetX, item.GetY, 0);
        }
    }

    private void UnlockGate(Item item)
    {
        if (IsTeamGate(item.Definition.InteractionType)) {
            _room.GetGameMap().SetFloorStatus(item.GetX, item.GetY, 1);
        }
    }

    public void StopGame()
    {
        StopGame(_clock.GetUtcNow());
    }

    internal void StopGame(DateTimeOffset stoppedAt) => _room.LastTimerResetAt = stoppedAt;

    public void Dispose()
    {
        Highscores.Dispose();
        Array.Clear(Points, 0, Points.Length);
        _redTeamItems.Clear();
        _blueTeamItems.Clear();
        _greenTeamItems.Clear();
        _yellowTeamItems.Clear();
        Points = null;
        _redTeamItems = null;
        _blueTeamItems = null;
        _greenTeamItems = null;
        _yellowTeamItems = null;
        _room = null;
    }
}
