using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The roller is 0.5 high. A raised roller stands on a 0.2 floor item, so its top is 0.7.
    [Theory]
    [InlineData(false, true, "0.7")]
    [InlineData(true, false, "0.5")]
    [InlineData(true, true, "0.7")]
    public void ARollerLandsTheUserOnTheTopOfTheNextRollerWhateverItsHeight(bool raisedStart, bool raisedTarget, string toZ)
    {
        if (raisedStart) {
            ExecutorFloor(20, 0, 1, height: 0.2);
        }

        if (raisedTarget) {
            ExecutorFloor(21, 1, 1, height: 0.2);
        }

        ExecutorRoller(10, 0, 1);
        ExecutorRoller(11, 1, 1);
        var actor = ExecutorRollerActor(0, 1, raisedStart ? 0.7 : 0.5);
        EnableExecutorRollers();
        ExecutorTick();
        Assert.Equal((0, 1, 1, 1, 10u, raisedStart ? "0.7" : "0.5", toZ), ExecutorAvatarSlide(actor));
        Assert.Equal((1, 1, double.Parse(toZ, System.Globalization.CultureInfo.InvariantCulture)), (actor.X, actor.Y, Math.Round(actor.Z, 3)));
    }
}
