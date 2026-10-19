using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void DiagonalRecoveryUsesLiveHeightsWhenBothStableSupportsWereLowered()
    {
        var source = ExecutorFloor(10, 1, 1, z: 2);
        var recovery = ExecutorFloor(11, 1, 2, z: 2);
        ExecutorFloor(12, 2, 2, height: 2);
        Add(13, 2, 1, height: 4, stackable: false);
        var actor = LayeredActor(1, 1);
        actor.SetPos(1, 1, 2);
        ExecutorTick();
        actor.MoveTo(2, 2);
        ParityAwaitAnnouncement(actor, 2, 2);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(source, 1, 1, 0.2));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(recovery, 1, 2, 0.2));
        Add(14, 2, 2, z: 2, height: 1, stackable: false);
        ExecutorTick();
        Assert.Equal((1, 1, 0.2), (actor.X, actor.Y, actor.Z));
        Assert.Contains("/mv 1,2,0.2/", ExecutorUpdate(actor).Status);
        ExecutorTick();

        Assert.Equal((1, 2, 0.2), (actor.X, actor.Y, actor.Z));
        Assert.Equal(new SurfaceRef(LayeredTile(1, 2), 11, SurfaceKind.Top), actor.Movement.CurrentRef);
        Assert.False(actor.HasStatus("mv"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboDiagonalRecoveryKeepsARemovedOriginalOrthogonalBlockerUnavailable(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(101, 7, 9);
        var actor = LayeredActor(5, 6, new() { LayeringEnabled = layered });
        var path = HabboParityWalk(actor, 8, 10, next =>
        {
            if (next == (6, 7)) {
                HabboParityMove(101, 9, 2);
            }

            if (next == (7, 8)) {
                HabboParityMove(100, 8, 9);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 6), (6, 7), (7, 8), (8, 8));
    }
}
