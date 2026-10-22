using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: Control Clock refuses a save with a negative id of no furni, alone or next to a
    // counter, and a wall item picked as a wall item, with require_counter_furni and no parameters. The box keeps its previous picks.
    [Theory]
    [InlineData((int)PickCounter, -(int)PickMissing)]
    [InlineData(-(int)PickMissing)]
    [InlineData(-(int)PickLamp)]
    public void ControlClockRefusesANegativePickThatIsNoCounter(params int[] picks)
    {
        var (f, owner, frames) = PickFixture();
        SavePicks(f, owner, frames, PickClock, [0], (int)PickCounter);
        Assert.Null(SaveRefusal(frames));

        SavePicks(f, owner, frames, PickClock, [0], picks);

        Assert.Equal("wiredfurni.error.require_counter_furni", SaveRefusal(frames));
        Assert.Equal([PickCounter], Picks(f, PickClock).ToArray());
    }
}
