using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void RoomTickOwnsTheNavigationScope()
    {
        var source = ExecutorFloor(10, 0, 1);
        var actor = ExecutorActor(0, 1);
        actor.UserId = 7;
        _room.GetGameMap().AddUserToMap(actor, actor.Coordinate);
        bool? owned = null;
        ReviewObserveWalkOff((_, item) =>
        {
            if (item == source) {
                owned = RoomOwnerScope.IsOwner(_room);
            }
        });
        actor.MoveTo(1, 1);
        ExecutorTick();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Equal(true, owned);
    }

    [Fact]
    public void FastWiredTickWaitsForTheNavigationMonitor()
    {
        Task tick;

        lock (_room.NavigationSync) {
            tick = Task.Run(_room.ProcessWiredOnly);
            Assert.False(tick.Wait(TimeSpan.FromMilliseconds(50)));
        }

        Assert.True(tick.Wait(TimeSpan.FromSeconds(3)));
        Assert.True(tick.IsCompletedSuccessfully);
    }
}
