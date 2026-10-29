using System.Reflection;
using Dapper;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // One player plays six rounds started and stopped by Control Clock on a football timer. Every board kind takes the round:
    // Classic keeps one entry per round with points, Per-team keeps the best points of the same players, Most Wins counts the
    // rounds with points, and the two time boards keep the shortest and the longest round. A round without a team counts nowhere.
    [WiredChestDatabaseFact]
    public void ControlClockRoundsWriteEveryBoardKind()
    {
        using var db = new WiredChestDatabaseTests.Fixture();
        typeof(PlacedFurniRoomTests).GetField("_database", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, db.Database);
        var clock = new HighscoreClock();
        Set("_interactionClock", clock);
        Set("_gamemap", new Gamemap(_room, new RoomModel("highscore", 0, 0, 0, 0, "00000\r00000\r00000\r00000\r00000", 0, 0, false),
            TestLogging.Navigation, new TestRoomSettings(new() { ["pathfinding.engine"] = "v2" }),
            TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance));
        Plus.HabboHotel.Rooms.Instance.WiredComponent wired = null!;
        RoomUser actor = null!;
        _room.RunFastPass(() => (wired, actor) = PrepareSpeech(clock));
        var boards = new[]
        {
            NamedBoard(511, "highscore_classic*1"), NamedBoard(512, "highscore_perteam*1"), NamedBoard(513, "highscore_mostwin*1"),
            NamedBoard(514, "highscore_fastesttime*1"), NamedBoard(515, "highscore_longesttime*1"), NamedBoard(516, "highscore_classic*2")
        };
        var timer = HighscoreTimer();

        foreach (var item in boards.Append(timer)) {
            db.Connection.Execute("INSERT INTO items(id,user_id,room_id,base_item,extra_data) VALUES(@id,7,42,@id,@payload)", new { item.Id, payload = item.ExtraData.Serialize() });
        }

        var data = new RoomDataComponent(Proxy<IRoomFurnitureLoader>((method, _) => method == "Load" ? boards.Append(timer).ToArray() : null));
        data.Initiate(_room);
        data.Initiated();
        _client.GetHabbo().Effects = new Plus.HabboHotel.Users.Effects.EffectsComponent(clock);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "start" }, 0);
        AddSpeechBox(302, "wf_act_control_clock", new() { IntParams = [0, 100], SelectedItems = [timer.Id] }, 1);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "stop" }, 0, 2, 2);
        AddSpeechBox(312, "wf_act_control_clock", new() { IntParams = [1, 100], SelectedItems = [timer.Id] }, 1, 2, 2);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "red5" }, 0, 3, 3);
        AddSpeechBox(322, "wf_act_give_score_tm", new() { IntParams = [5, 0, (int)Team.Red] }, 1, 3, 3);
        AddSpeechBox(331, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "red3" }, 0, 4, 4);
        AddSpeechBox(332, "wf_act_give_score_tm", new() { IntParams = [3, 0, (int)Team.Red] }, 1, 4, 4);
        AddSpeechBox(341, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "blue5" }, 0, 0, 4);
        AddSpeechBox(342, "wf_act_give_score_tm", new() { IntParams = [5, 0, (int)Team.Blue] }, 1, 0, 4);
        var name = actor.GetUsername();
        var gameState = WiredGameState.For(_room);

        void Round(Team team, int seconds, params string[] scores)
        {
            if (team == Team.None) {
                gameState.Leave(_room, actor);
            }
            else if (actor.Team != team) {
                Assert.True(gameState.Join(_room, actor, 0, team, 0, [actor]));
            }

            actor.OnChat(0, "start", false);

            foreach (var score in scores) {
                actor.OnChat(0, score, false);
            }

            clock.Now += TimeSpan.FromSeconds(seconds);
            actor.OnChat(0, "stop", false);
        }

        string[] Entries(Item board) => ((HighscoreDataFormat)board.ExtraData).Entries.Select(entry => entry.Score + ":" + string.Join(",", entry.Users)).ToArray();

        Round(Team.Red, 8, "red5", "red5");
        Round(Team.Red, 6, "red3");
        Round(Team.Red, 18, "red5", "red5", "red5");
        Round(Team.Blue, 6, "blue5");
        Round(Team.Red, 4);
        Round(Team.None, 3, "red5");

        Assert.Equal([$"15:{name}", $"10:{name}", $"5:{name}", $"3:{name}"], Entries(boards[0]));
        Assert.Equal([$"15:{name}"], Entries(boards[1]));
        Assert.Equal([$"4:{name}"], Entries(boards[2]));
        Assert.Equal([$"4:{name}"], Entries(boards[3]));
        Assert.Equal([$"18:{name}"], Entries(boards[4]));
        Assert.Equal(Entries(boards[0]), Entries(boards[5]));
        Assert.All(boards, board => Assert.Equal(board.ExtraData.Serialize(),
            db.Connection.QuerySingle<string>("SELECT extra_data FROM items WHERE id=@Id", new { board.Id })));
    }

    // A board shown with state 1 keeps its state when a round writes its scores.
    [WiredChestDatabaseFact]
    public void AnOpenBoardStaysOpenWhenARoundWritesIt()
    {
        using var db = new WiredChestDatabaseTests.Fixture();
        var game = PrepareHighscoreGame(db);
        ((HighscoreDataFormat)game.Wins.ExtraData).State = "1";
        db.Connection.Execute("UPDATE items SET extra_data=@payload WHERE id=502", new { payload = game.Wins.ExtraData.Serialize() });

        game.Actor.OnChat(0, "start", false);
        game.Clock.Now += TimeSpan.FromSeconds(3);
        game.Actor.OnChat(0, "stop", false);

        var wins = (HighscoreDataFormat)game.Wins.ExtraData;
        Assert.Equal("1", wins.State);
        Assert.Equal(1, Assert.Single(wins.Entries).Score);
    }

    // A board opened in the room has state 1 live while its saved row still says 0. A round still writes it.
    [WiredChestDatabaseFact]
    public void ABoardWhoseSavedStateLagsItsLiveStateTakesTheRound()
    {
        using var db = new WiredChestDatabaseTests.Fixture();
        var game = PrepareHighscoreGame(db);
        ((HighscoreDataFormat)game.Wins.ExtraData).State = "1";

        game.Actor.OnChat(0, "start", false);
        game.Clock.Now += TimeSpan.FromSeconds(3);
        game.Actor.OnChat(0, "stop", false);

        var wins = (HighscoreDataFormat)game.Wins.ExtraData;
        Assert.Equal(1, Assert.Single(wins.Entries).Score);
        Assert.Equal(wins.Serialize(), db.Connection.QuerySingle<string>("SELECT extra_data FROM items WHERE id=502"));
    }

    // A bought board keeps an empty row until the room saves it. A round still writes it, and the other boards too.
    [WiredChestDatabaseFact]
    public void ABoardWithAnEmptySavedRowTakesTheRound()
    {
        using var db = new WiredChestDatabaseTests.Fixture();
        var game = PrepareHighscoreGame(db);
        db.Connection.Execute("UPDATE items SET extra_data='' WHERE id=501");

        game.Actor.OnChat(0, "start", false);
        game.Clock.Now += TimeSpan.FromSeconds(3);
        game.Actor.OnChat(0, "stop", false);

        Assert.Equal(3, Assert.Single(((HighscoreDataFormat)game.Fastest.ExtraData).Entries).Score);
        Assert.Equal(1, Assert.Single(((HighscoreDataFormat)game.Wins.ExtraData).Entries).Score);
        Assert.Equal(game.Fastest.ExtraData.Serialize(), db.Connection.QuerySingle<string>("SELECT extra_data FROM items WHERE id=501"));
    }

    private Item NamedBoard(uint id, string itemName)
    {
        HighscoreDataFormat.TryDefinition(itemName, out var scoreType, out var clearType);
        var board = HighscoreBoard(id, scoreType);
        board.Definition.ItemName = itemName;
        board.ExtraData = new HighscoreDataFormat { State = "0", ScoreType = scoreType, ClearType = clearType };

        return board;
    }
}
