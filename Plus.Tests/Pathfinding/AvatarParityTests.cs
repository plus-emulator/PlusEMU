using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void InitialPlanningStillAvoidsRollerReservedTransitTiles()
    {
        var (grid, _, _) = Pathfinding.NavTest.Create(3, 1);
        var occupancy = new PlanningOccupancy(grid.SlotCapacity);
        occupancy.Targets[1] = TargetOccupancy.RollerClaim;
        var actor = new ActorProfile { Walkthrough = true };
        using var lease = PathWorkspacePool.Rent(grid.SlotCapacity, grid.ActiveNodeCount);
        var outcome = new PathSearch(grid, new()).Find(new(actor, new(0, 0, 0), 2, 0,
            occupancy, View: OccupancyView.InitialPlanning), lease.Workspace, new());

        Assert.Equal(PathOutcome.Unreachable, outcome);
    }

    [Fact]
    public void RepairOccupancyDoesNotFollowAReusedSlotOrFallToAnotherLevel()
    {
        var deck = ExecutorFloor(10, 1, 1, z: 2);
        var actor = LayeredActor(0, 1);
        var grid = LayeredNavigation.Grid;
        var original = new SurfaceRef(LayeredTile(1, 1), 10, SurfaceKind.Top);
        var snapshot = grid.CreateSnapshot();
        var originalSlot = snapshot.SlotOf(original);
        Assert.True(originalSlot >= grid.TileCount);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(deck, 2, 1, 3));
        ExecutorAdditionalBot(1, 1, 5);
        var movedDeckOccupant = ExecutorAdditionalBot(2, 1, 6);
        movedDeckOccupant.SetPos(2, 1, 3);
        ExecutorTick();
        Assert.Equal(originalSlot, grid.SlotOf(new(LayeredTile(2, 1), 10, SurfaceKind.Top)));
        Assert.Equal(-1, grid.SlotOf(original));
        var occupancy = LayeredNavigation.Executor.Context.Occupancy(actor, snapshot);

        Assert.Equal(TargetOccupancy.None, occupancy.Targets[originalSlot]);
        Assert.Equal(TargetOccupancy.Stationary,
            occupancy.Targets[snapshot.SlotOf(new(LayeredTile(1, 1), 0, SurfaceKind.Floor))]);
        Assert.Equal(TargetOccupancy.None,
            occupancy.Targets[snapshot.SlotOf(new(LayeredTile(2, 1), 0, SurfaceKind.Floor))]);
    }

    [Fact]
    public void RepairOccupancyFollowsAStableSurfaceWhoseHeightChanged()
    {
        var deck = ExecutorFloor(10, 1, 1, z: 2);
        var actor = LayeredActor(0, 1);
        var occupant = ExecutorAdditionalBot(1, 1, 5);
        occupant.SetPos(1, 1, 2);
        ExecutorTick();
        var snapshot = LayeredNavigation.Grid.CreateSnapshot();
        var original = new SurfaceRef(LayeredTile(1, 1), 10, SurfaceKind.Top);
        Assert.Equal(original, occupant.Movement.CurrentRef);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(deck, 1, 1, 2.5));
        ExecutorTick();
        Assert.Equal(2.5, occupant.Z);
        var occupancy = LayeredNavigation.Executor.Context.Occupancy(actor, snapshot);

        Assert.Equal(TargetOccupancy.Stationary, occupancy.Targets[snapshot.SlotOf(original)]);
        Assert.Equal(TargetOccupancy.None,
            occupancy.Targets[snapshot.SlotOf(new(LayeredTile(1, 1), 0, SurfaceKind.Floor))]);
    }

    [Fact]
    public void RepairOccupancyFindsAReplacementSurfaceAtTheSameHeight()
    {
        ExecutorFloor(10, 1, 1, z: 2);
        var actor = LayeredActor(0, 1);
        var snapshot = LayeredNavigation.Grid.CreateSnapshot();
        var original = new SurfaceRef(LayeredTile(1, 1), 10, SurfaceKind.Top);
        _room.GetRoomItemHandler().RemoveFurniture(null!, 10);
        ExecutorFloor(11, 1, 1, z: 2);
        var occupant = ExecutorAdditionalBot(1, 1, 5);
        occupant.SetPos(1, 1, 2);
        ExecutorTick();
        Assert.Equal(-1, LayeredNavigation.Grid.SlotOf(original));
        Assert.Equal(new SurfaceRef(LayeredTile(1, 1), 11, SurfaceKind.Top), occupant.Movement.CurrentRef);
        var occupancy = LayeredNavigation.Executor.Context.Occupancy(actor, snapshot);

        Assert.Equal(TargetOccupancy.Stationary, occupancy.Targets[snapshot.SlotOf(original)]);
        Assert.Equal(TargetOccupancy.None,
            occupancy.Targets[snapshot.SlotOf(new(LayeredTile(1, 1), 0, SurfaceKind.Floor))]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HabboInitialRouteAvoidsAStationaryAvatarEvenWhenWalkthroughIsEnabled(bool layered, bool walkthrough)
    {
        var actor = HabboParityActor(7, 7, layered);
        _room.RoomBlockingEnabled = walkthrough;
        var occupant = ExecutorAdditionalBot(7, 10, 5);
        ExecutorTick();
        var path = HabboParityWalk(actor, 7, 12);

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (6, 10), (7, 11), (7, 12));
        Assert.Equal((7, 10), (occupant.X, occupant.Y));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void HabboRepairUsesCurrentAvatarOccupancyAndTheWalkthroughSetting(bool layered, bool walkthrough, bool leaves)
    {
        HabboParityRoom();
        HabboParityMove(103, 8, 10);
        _room.RoomBlockingEnabled = walkthrough;
        var actor = LayeredActor(7, 7, new() { LayeringEnabled = layered });
        var occupant = ExecutorAdditionalBot(6, 10, 5);
        ExecutorTick();
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (leaves && next == (7, 8)) {
                occupant.SetPos(9, 8, 0);
            }

            if (next == (7, 9)) {
                HabboParityMove(100, 7, 10);
            }
        });

        if (walkthrough || leaves) {
            HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (6, 10), (7, 11), (7, 12));
        }
        else {
            HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9));
        }

        Assert.Equal(leaves ? (9, 8) : (6, 10), (occupant.X, occupant.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AStandingAvatarTileReopensOnceTheAvatarWalksAwayAfterAMapRebuild(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        _room.RoomBlockingEnabled = false;
        ExecutorTick();
        _room.GetGameMap().GenerateMaps();
        var path = HabboParityWalk(actor, 7, 9);

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9));
        Assert.True(_room.GetGameMap().SquareIsOpen(7, 7, false));
    }
}
