using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(3, 3, 2)]
    [InlineData(2, 1, 4)]
    public void BanzaiStepThatDoesNotKickLeavesPuckInPlace(int puckX, int puckY, int rotation)
    {
        Assert.Equal((puckX, puckY), PuckAfterStep(puckX, puckY, rotation));
    }

    [Theory]
    [InlineData(2, 1, 2, 3, 1)]
    [InlineData(1, 1, 2, 3, 1)]
    [InlineData(1, 1, 6, 2, 1)]
    public void BanzaiStepThatKicksPuckMovesIt(int puckX, int puckY, int rotation, int expectedX, int expectedY)
    {
        Assert.Equal((expectedX, expectedY), PuckAfterStep(puckX, puckY, rotation));
    }

    private (int X, int Y) PuckAfterStep(int puckX, int puckY, int rotation)
    {
        OpenCornerPitch();
        var puck = Add(10, puckX, puckY, type: InteractionType.Banzaipuck);
        _room.GetBanzai().AddPuck(puck);
        var actor = LandingActor();
        actor.RotBody = rotation;
        new LandingEffects(_room, _database).Apply(actor, false);

        return (puck.GetX, puck.GetY);
    }
}
