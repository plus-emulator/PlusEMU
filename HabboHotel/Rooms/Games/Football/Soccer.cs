using System.Collections.Concurrent;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Rooms.Games.Football;

public class Soccer
{
    private ConcurrentDictionary<uint, Item> _balls;
    private Item[] _gates;
    private Room _room;

    public Soccer(Room room)
    {
        _room = room;
        _gates = new Item[4];
        _balls = new();
        GameIsStarted = false;
    }

    public bool GameIsStarted { get; private set; }

    public void StopGame(bool triggeredByUser = false)
    {
        GameIsStarted = false;

        if (!triggeredByUser) {
            _room.GetWired().TriggerEvent(WiredBoxType.TriggerGameEnds, null);
        }
    }

    public void StartGame()
    {
        GameIsStarted = true;
    }

    public void AddBall(Item item)
    {
        _balls.TryAdd(item.Id, item);
    }

    public void RemoveBall(uint itemId)
    {
        _balls.TryRemove(itemId, out var item);
    }

    public void OnUserWalk(RoomUser user)
    {
        if (user == null) {
            return;
        }

        foreach (var item in _balls.Values.ToList()) {
            if (BallKick.Target(_room, user, item) is { } target) {
                MoveBall(item, target.X, target.Y, user);
            }
        }
    }

    public void RegisterGate(Item item)
    {
        if (_gates[0] == null) {
            item.Team = Team.Blue;
            _gates[0] = item;
        }
        else if (_gates[1] == null) {
            item.Team = Team.Red;
            _gates[1] = item;
        }
        else if (_gates[2] == null) {
            item.Team = Team.Green;
            _gates[2] = item;
        }
        else if (_gates[3] == null) {
            item.Team = Team.Yellow;
            _gates[3] = item;
        }
    }

    public void UnRegisterGate(Item item)
    {
        switch (item.Team) {
            case Team.Blue: {
                    _gates[0] = null;
                    break;
                }
            case Team.Red: {
                    _gates[1] = null;
                    break;
                }
            case Team.Green: {
                    _gates[2] = null;
                    break;
                }
            case Team.Yellow: {
                    _gates[3] = null;
                    break;
                }
        }
    }

    public void OnGateRemove(Item item)
    {
        switch (item.Definition.InteractionType) {
            case InteractionType.FootballGoalRed:
            case InteractionType.Footballcounterred: {
                    _room.GetGameManager().RemoveFurnitureFromTeam(item, Team.Red);
                    break;
                }
            case InteractionType.FootballGoalGreen:
            case InteractionType.Footballcountergreen: {
                    _room.GetGameManager().RemoveFurnitureFromTeam(item, Team.Green);
                    break;
                }
            case InteractionType.FootballGoalBlue:
            case InteractionType.Footballcounterblue: {
                    _room.GetGameManager().RemoveFurnitureFromTeam(item, Team.Blue);
                    break;
                }
            case InteractionType.FootballGoalYellow:
            case InteractionType.Footballcounteryellow: {
                    _room.GetGameManager().RemoveFurnitureFromTeam(item, Team.Yellow);
                    break;
                }
        }
    }

    public void MoveBall(Item item, int newX, int newY, RoomUser user)
    {
        if (item == null || user == null) {
            return;
        }

        if (!_room.GetGameMap().ItemCanBePlaced(newX, newY)) {
            return;
        }

        var oldRoomCoord = item.Coordinate;

        if (oldRoomCoord.X == newX && oldRoomCoord.Y == newY) {
            return;
        }

        double newZ = _room.GetGameMap().Model.SqFloorHeight[newX, newY];
        _room.SendPacket(new SlideObjectBundleComposer(item.Coordinate.X, item.Coordinate.Y, item.GetZ, newX, newY, newZ, item.Id, (int)item.Id, item.Id));
        item.LegacyDataString = "11";
        item.UpdateNeeded = true;
        _room.GetRoomItemHandler().SetFloorItem(null, item, newX, newY, item.Rotation, false, false, false);
        _room.OnUserShoot(user, item);
    }

    public void Dispose()
    {
        Array.Clear(_gates, 0, _gates.Length);
        _gates = null;
        _room = null;
        _balls.Clear();
        _balls = null;
    }
}
