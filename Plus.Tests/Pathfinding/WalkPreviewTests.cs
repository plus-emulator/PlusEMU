using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void WalkPreviewShowsTheDetourWithoutMovingTheAvatar()
    {
        FallbackModel(FallbackAlternative);
        Add(21, 3, 1, height: 1, stackable: false);
        var actor = FallbackActor(0, 1);
        var route = _room.GetGameMap().Navigation!.PreviewWalk(actor, 6, 1);

        Assert.Contains(route, p => p.Y == 3);
        Assert.DoesNotContain((3, 1), route);
        Assert.Equal((6, 1), route[^1]);
        Assert.False(actor.Movement.HasIntent);
        Assert.False(actor.SetStep);
        actor.MoveTo(6, 1);
        var walked = FallbackRun(actor, 40).Select(p => (p.X, p.Y)).Distinct().Where(p => p != (0, 1));
        Assert.Equal(route, walked);
    }
    [Fact]
    public void WalkPreviewDoesNotReturnAPartialRouteToAnUnreachableTile()
    {
        FallbackModel(FallbackDeadEnd);
        var actor = FallbackActor(0, 1);

        Assert.Empty(_room.GetGameMap().Navigation!.PreviewWalk(actor, 3, 3));
        Assert.False(actor.Movement.HasIntent);
    }
}
