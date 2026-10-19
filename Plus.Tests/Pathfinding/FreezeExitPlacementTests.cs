using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void FreezeStopKeepsPositionAndOccupancyTogetherUntilTheOwnerDrains()
    {
        var gameTile = ExecutorFloor(10, 1, 1);
        var exit = ExecutorFloor(11, 3, 3);
        var freeze = _room.GetFreeze();
        freeze.AddFreezeTile(gameTile);
        freeze.AddExitTile(exit);
        var actor = ExecutorActor(1, 1);
        var map = _room.GetGameMap();
        Assert.False(RoomOwnerScope.IsOwner(_room));

        freeze.StopGame(userTriggered: true);

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Contains(actor, map.GetRoomUsers(new(1, 1)));
        Assert.DoesNotContain(actor, map.GetRoomUsers(new(3, 3)));

        using (RoomOwnerScope.Enter(_room)) {
            map.Navigation.DrainCommands();
        }

        Assert.Equal((3, 3), (actor.X, actor.Y));
        Assert.Contains(actor, map.GetRoomUsers(new(3, 3)));
        Assert.DoesNotContain(actor, map.GetRoomUsers(new(1, 1)));
    }
}
