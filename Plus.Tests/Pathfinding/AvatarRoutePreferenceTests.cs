using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboInitialWalkthroughCrossesAStationaryAvatarWhenItIsTheOnlyRoute(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(100, 6, 11);
        var actor = LayeredActor(7, 12, new() { LayeringEnabled = layered });
        var occupant = ExecutorAdditionalBot(6, 12, 5);
        ExecutorTick();
        var path = HabboParityWalk(actor, 5, 12);

        HabboParityAssertStopped(actor, path, (7, 12), (6, 12), (5, 12));
        Assert.Equal((6, 12), (occupant.X, occupant.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboInitialWalkthroughPrefersAnOccupiedTwoStepRouteOverAFreeFourStepDetour(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(100, 6, 7);
        HabboParityMove(101, 6, 9);
        var actor = LayeredActor(5, 8, new() { LayeringEnabled = layered });
        var occupant = ExecutorAdditionalBot(6, 8, 5);
        ExecutorTick();
        var path = HabboParityWalk(actor, 7, 8);

        HabboParityAssertStopped(actor, path, (5, 8), (6, 8), (7, 8));
        Assert.Equal((6, 8), (occupant.X, occupant.Y));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboInitialWalkthroughPrefersAFreeThreeStepDetourOverAnOccupiedTwoStepRoute(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(100, 5, 8);
        HabboParityMove(101, 6, 9);
        var actor = LayeredActor(5, 7, new() { LayeringEnabled = layered });
        var occupant = ExecutorAdditionalBot(6, 8, 5);
        ExecutorTick();
        var path = HabboParityWalk(actor, 7, 9);

        HabboParityAssertStopped(actor, path, (5, 7), (6, 7), (7, 8), (7, 9));
        Assert.Equal((6, 8), (occupant.X, occupant.Y));
    }
}
