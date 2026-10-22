using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(3, 3, 2)]
    [InlineData(2, 1, 4)]
    public void SoccerStepThatDoesNotKickLeavesBallInPlace(int ballX, int ballY, int rotation)
    {
        OpenCornerPitch();
        var ball = Add(10, ballX, ballY);
        _room.GetSoccer().AddBall(ball);
        var actor = LandingActor();
        actor.RotBody = rotation;
        new LandingEffects(_room, _database).Apply(actor, false);
        Assert.Equal((ballX, ballY), (ball.GetX, ball.GetY));
    }

    [Fact]
    public void SoccerStepNextToBallFacingItDribblesItOneTile()
    {
        OpenCornerPitch();
        var ball = Add(10, 2, 1);
        _room.GetSoccer().AddBall(ball);
        var actor = LandingActor();
        actor.RotBody = 2;
        new LandingEffects(_room, _database).Apply(actor, false);
        Assert.Equal((3, 1), (ball.GetX, ball.GetY));
    }

    // Tile (0,0) must be open floor, not the door, for a stray move there to be observable.
    private void OpenCornerPitch()
    {
        Set("_gamemap", new Gamemap(_room, new RoomModel("pitch", 0, 3, 0, 0, "0000\r0000\r0000\r0000", 0, 0, false),
            TestLogging.Navigation, _roomSettings, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance));
        _room.GetGameMap().GenerateMaps();
    }
}
