using System.Collections.Concurrent;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void FreezeBlastAfterTheGameEndedDoesNotHitScoreOrRemovePlayers()
    {
        var ends = CountFreezeGameEnds();
        var tile = FreezeTile(10, 1, 1);
        var blue = FreezePlayer(Viewer(1, 1), Team.Blue);
        FreezePlayer(SecondFreezeUser(3, 3), Team.Red);
        var freeze = _room.GetFreeze();
        freeze.StartGame();
        freeze.StopGame();

        freeze.OnFreezeTiles(tile, FreezePowerUp.None);
        RunFreezeWired();

        Assert.Equal(0, blue.FreezeLives);
        Assert.False(blue.Freezed);
        Assert.Equal(Team.Blue, blue.Team);
        Assert.Contains(blue, _room.GetTeamManagerForFreeze().BlueTeam);
        Assert.Equal(30, _room.GetGameManager().Points[(int)Team.Blue]);
        Assert.Equal(1, ends());
    }

    [Fact]
    public void FreezeBlastThatEndsTheGameDoesNotHitItsRemainingVictims()
    {
        var ends = CountFreezeGameEnds();
        var tile = FreezeTile(10, 1, 1);
        FreezeTile(11, 2, 1);
        var red = FreezePlayer(Viewer(1, 1), Team.Red);
        var blue = FreezePlayer(SecondFreezeUser(2, 1), Team.Blue);
        var freeze = _room.GetFreeze();
        freeze.StartGame();
        red.FreezeLives = 1;

        freeze.OnFreezeTiles(tile, FreezePowerUp.None);
        RunFreezeWired();

        Assert.False(freeze.GameIsStarted);
        Assert.Equal(Team.None, red.Team);
        Assert.Equal(0, blue.FreezeLives);
        Assert.False(blue.Freezed);
        Assert.Equal(Team.Blue, blue.Team);
        Assert.Contains(blue, _room.GetTeamManagerForFreeze().BlueTeam);
        Assert.Equal(30, _room.GetGameManager().Points[(int)Team.Blue]);
        Assert.Equal(1, ends());
    }

    private Item FreezeTile(uint id, int x, int y)
    {
        var tile = Add(id, x, y, type: InteractionType.FreezeTile);
        _room.GetFreeze().AddFreezeTile(tile);

        return tile;
    }

    private RoomUser FreezePlayer(RoomUser user, Team team)
    {
        _room.GetGameMap().AddUserToMap(user, user.Coordinate);
        user.Team = team;
        _room.GetTeamManagerForFreeze().AddUser(user);

        return user;
    }

    private RoomUser SecondFreezeUser(int x, int y)
    {
        var client = new TestClient();
        client.SetHabbo(new Habbo { Id = 9, Username = "rival", CurrentRoom = _room, Access = UserAccess.Empty });
        var user = new RoomUser(9, RoomId, 2, _room, client, TestChatEmotions.Unused, new TestRewardProgress()) { X = x, Y = y };
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomUserManager())!;
        users.TryAdd(2, user);

        return user;
    }

    private Func<int> CountFreezeGameEnds()
    {
        var engine = (WiredStackEngine)typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetWired())!;
        var observe = engine.ObserveEvent;
        var ends = 0;
        engine.ObserveEvent = (evt, now) =>
        {
            if (evt.Kind == WiredEventKind.GameEnd) {
                ends++;
            }

            observe?.Invoke(evt, now);
        };

        return () => ends;
    }

    private void RunFreezeWired()
    {
        for (var i = 0; i < 3; i++) {
            _room.GetWired().OnFastCycle();
        }
    }
}
