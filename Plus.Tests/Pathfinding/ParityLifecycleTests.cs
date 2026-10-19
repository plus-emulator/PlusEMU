using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void ParityLoweredSourceCanReachTheNextTileThroughAnotherSurface()
    {
        FallbackModel("0000");
        ExecutorFloor(10, 0, 0, z: 2);
        var source = ExecutorFloor(11, 1, 0, z: 2);
        var alternative = ExecutorFloor(12, 2, 0, z: 0.1);
        ExecutorFloor(13, 2, 0, z: 2);
        ExecutorFloor(14, 3, 0, height: 2.8);
        var actor = LayeredActor(0, 0);
        actor.SetPos(0, 0, 2);
        ExecutorTick();
        actor.MoveTo(3, 0);
        ParityAwaitAnnouncement(actor, 2, 0);
        Assert.Equal((1, 0, 2d), (actor.X, actor.Y, actor.Z));
        Assert.Equal(new SurfaceRef(LayeredTile(2, 0), 13, SurfaceKind.Top), actor.Movement.Pending[0]);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(source, 1, 0, 0.2));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(alternative, 2, 0, 1.4));
        ExecutorTick();
        Assert.Equal((2, 0, 1.4), (actor.X, actor.Y, actor.Z));
        Assert.Equal(new SurfaceRef(LayeredTile(2, 0), 12, SurfaceKind.Top), actor.Movement.CurrentRef);
        ExecutorTick();
        Assert.Equal((3, 0, 2.8), (actor.X, actor.Y, actor.Z));
        Assert.False(actor.HasStatus("mv"));
    }

    [Fact]
    public void ParityLoweredSupportKeepsTheRetainedRouteThroughAnotherSurface()
    {
        FallbackModel("0000");
        ExecutorFloor(10, 0, 0, z: 2);
        ExecutorFloor(11, 1, 0, z: 2);
        var alternative = ExecutorFloor(12, 2, 0, z: 0.1);
        var support = ExecutorFloor(13, 2, 0, z: 2);
        ExecutorFloor(14, 3, 0, height: 3);
        var actor = LayeredActor(0, 0);
        actor.SetPos(0, 0, 2);
        ExecutorTick();
        actor.MoveTo(3, 0);
        ParityAwaitAnnouncement(actor, 1, 0);
        Assert.Equal(new SurfaceRef(LayeredTile(2, 0), 13, SurfaceKind.Top), actor.Movement.Route.Steps[1]);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(support, 2, 0, 0.1));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(alternative, 2, 0, 1.7));
        ExecutorTick();
        Assert.Equal((1, 0, 2d), (actor.X, actor.Y, actor.Z));
        Assert.Contains("/mv 2,0,1.7/", ExecutorUpdate(actor).Status);
        ExecutorTick();
        Assert.Equal((2, 0, 1.7), (actor.X, actor.Y, actor.Z));
        Assert.Equal(new SurfaceRef(LayeredTile(2, 0), 12, SurfaceKind.Top), actor.Movement.CurrentRef);
        ExecutorTick();
        Assert.Equal((3, 0, 3d), (actor.X, actor.Y, actor.Z));
        Assert.False(actor.HasStatus("mv"));
    }

    [Fact]
    public void ParityLoweredSupportDetoursAroundAStepThatBecameTooHigh()
    {
        FallbackModel("0000\r0000");
        ExecutorFloor(10, 0, 0, height: 2);
        var support = ExecutorFloor(11, 1, 0, z: 2);
        ExecutorFloor(12, 2, 0, height: 2);
        ExecutorFloor(13, 3, 0, height: 1);
        var actor = LayeredActor(0, 0);
        actor.SetPos(0, 0, 2);
        ExecutorTick();
        actor.MoveTo(3, 0);
        ParityAwaitAnnouncement(actor, 1, 0);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(support, 1, 0, 0.1));
        var path = new List<(int X, int Y, double Z)> { (actor.X, actor.Y, actor.Z) };

        for (var tick = 0; tick < 12 && (actor.X, actor.Y) != (3, 0); tick++) {
            ExecutorTick();

            if ((path[^1].X, path[^1].Y) != (actor.X, actor.Y)) {
                path.Add((actor.X, actor.Y, actor.Z));
            }
        }

        Assert.Equal([(0, 0, 2), (1, 0, 0), (2, 1, 0), (3, 0, 1)], path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParityReplacementClickSkipsAChairIntroducedAtTheOldGoal(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        actor.MoveTo(7, 10);
        ParityAwaitAnnouncement(actor, 7, 9);
        HabboParityMove(103, 7, 10);
        ParityAwaitAnnouncement(actor, 7, 10);
        Assert.Equal((7, 9), (actor.X, actor.Y));
        actor.MoveTo(8, 9);
        ExecutorTick();
        Assert.Equal((7, 9), (actor.X, actor.Y));
        Assert.False(actor.HasStatus("sit"));
        Assert.Contains("/mv 8,9,0.0/", ExecutorUpdate(actor).Status);
        ExecutorTick();
        FallbackAssertStopped(actor, 8, 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParityStopAfterStepEndsAtTheAnnouncedDiagonalRecovery(bool layered)
    {
        var actor = ParityAnnouncedRecovery(layered);
        var stopped = false;
        var navigation = _room.GetGameMap().Navigation!;
        navigation.RunOwner(actor, (user, _) => stopped = navigation.StopAfterStep(user));
        ExecutorTick();
        Assert.True(stopped);
        Assert.Equal((6, 9, 0.01), (actor.X, actor.Y, actor.Z));

        for (var tick = 0; tick < 5; tick++) {
            ExecutorTick();
        }

        FallbackAssertStopped(actor, 6, 9);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParityRejectedRecoveryCanUseTheOtherOrthogonalStep(bool layered)
    {
        var actor = ParityAnnouncedRecovery(layered);
        HabboParityMove(101, 6, 9);
        ExecutorTick();
        Assert.Equal((6, 8), (actor.X, actor.Y));
        Assert.Contains("/mv 7,8,0.0/", ExecutorUpdate(actor).Status);

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            Assert.NotEqual((6, 9), (actor.X, actor.Y));
            Assert.NotEqual((7, 9), (actor.X, actor.Y));
        }

        FallbackAssertStopped(actor, 8, 10);
    }

    [Fact]
    public void ParityTemporaryClaimWaitClearsThePreviousMovementAnnouncement()
    {
        FallbackModel("0000\r0000\r0000");
        _room.RoomBlockingEnabled = false;
        var actor = FallbackActor(0, 1, new() { LayeringEnabled = false });
        var owner = ExecutorAdditionalBot(3, 2, 2);
        ExecutorTick();
        actor.MoveTo(3, 1);
        ParityAwaitAnnouncement(actor, 1, 1);
        var navigation = _room.GetGameMap().Navigation!;
        var slot = navigation.Grid.Tile(2, 1);
        Assert.True(navigation.Executor.Claims.TryClaim(owner, slot, ClaimKind.Exclusive, TargetOccupancy.None));
        ExecutorTick();
        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.False(actor.HasStatus("mv"));
        Assert.DoesNotContain("/mv ", ExecutorUpdate(actor).Status);
        navigation.Executor.Claims.ReleaseBatch(owner);

        for (var tick = 0; tick < 6; tick++) {
            ExecutorTick();
        }

        FallbackAssertStopped(actor, 3, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParityNearestDestinationSurvivesABoundedReplan(bool layered)
    {
        FallbackModel("000000\r000000\r000000");
        Add(10, 5, 1, height: 2, stackable: false);
        var actor = FallbackActor(0, 1, new() { LayeringEnabled = layered, UnreachablePolicy = "nearest" });
        actor.MoveTo(5, 1);
        ExecutorTick();
        ExecutorTick();
        Assert.True(actor.HasStatus("mv"));
        Assert.Equal((4, 1), (actor.GoalX, actor.GoalY));
        var blocked = (actor.SetX, actor.SetY);
        Assert.NotEqual((4, 1), blocked);
        Add(11, blocked.SetX, blocked.SetY, height: 2, stackable: false);

        for (var tick = 0; tick < 12; tick++) {
            ExecutorTick();
            Assert.NotEqual(blocked, (actor.X, actor.Y));
            Assert.NotEqual((5, 1), (actor.X, actor.Y));
        }

        FallbackAssertStopped(actor, 4, 1);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ParityFastWalkPartialCommitLeavesAllowanceForUncommittedEdges(bool layered, bool superFast)
    {
        var actor = HabboParityActor(7, 7, layered);
        actor.FastWalking = !superFast;
        actor.SuperFastWalking = superFast;
        var blockedY = superFast ? 10 : 9;
        actor.MoveTo(7, 12);
        ParityAwaitAnnouncement(actor, 7, blockedY);
        HabboParityMove(100, 7, blockedY);
        ExecutorTick();
        Assert.Equal((7, blockedY - 1), (actor.X, actor.Y));

        for (var tick = 0; tick < 10; tick++) {
            ExecutorTick();
            Assert.NotEqual((7, blockedY), (actor.X, actor.Y));
        }

        FallbackAssertStopped(actor, 7, 12);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParityMountedDiagonalRecoveryKeepsHorseAndRiderTogether(bool layered)
    {
        var actor = HabboParityActor(5, 7, layered);
        var horse = ExternalLifecycleHorse(5, 7);
        ExecutorTick();
        EstablishExternalMountedGroup(actor, horse);
        var path = HabboParityWalk(actor, 8, 10, next =>
        {
            Assert.Equal((actor.X, actor.Y), (horse.X, horse.Y));
            Assert.Equal(horse.Z + 1, actor.Z);

            if (next == (6, 8)) {
                HabboParityMove(100, 7, 9);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 7), (6, 8), (6, 9), (7, 10), (8, 10));
        Assert.Equal((8, 10, 0d), (horse.X, horse.Y, horse.Z));
        Assert.Equal(1d, actor.Z);
        Assert.False(horse.HasStatus("mv"));
        Assert.False(horse.SetStep);
    }

    private RoomUser ParityAnnouncedRecovery(bool layered)
    {
        var actor = HabboParityActor(5, 7, layered);
        actor.MoveTo(8, 10);
        ParityAwaitAnnouncement(actor, 6, 8);
        HabboParityMove(100, 7, 9);
        ParityAwaitAnnouncement(actor, 6, 9);
        Assert.Equal((6, 8), (actor.X, actor.Y));

        return actor;
    }

    private void ParityAwaitAnnouncement(RoomUser actor, int x, int y)
    {
        for (var tick = 0; tick < 12; tick++) {
            ExecutorTick();

            if (actor.Statusses.TryGetValue("mv", out var movement)
                && movement.StartsWith($"{x},{y},", StringComparison.Ordinal)) {
                return;
            }
        }

        Assert.Fail($"No movement announcement toward {x},{y}; actor stopped at {actor.X},{actor.Y}.");
    }
}
