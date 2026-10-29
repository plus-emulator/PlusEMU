using Xunit;

namespace Plus.Tests;

// A Repeat placed at any moment fires on the ticks of the one already running, not on a grid of its own.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_trg_periodically", 2, 125)]
    [InlineData("wf_trg_periodically", 2, 275)]
    [InlineData("wf_trg_periodically", 2, 375)]
    [InlineData("wf_trg_periodically", 2, 475)]
    [InlineData("wf_trg_periodically", 2, 1125)]
    [InlineData("wf_trg_period_long", 1, 125)]
    [InlineData("wf_trg_period_long", 1, 375)]
    [InlineData("wf_trg_period_long", 1, 2275)]
    public void ARepeaterPlacedBetweenTwoRoomTicksFiresOnTheTicksOfTheOneAlreadyRunning(string name, int units, int placedAfterMs)
    {
        var (clock, manager) = GridRoom();
        var running = ToggleAt(20, 2, 0);
        var placed = ToggleAt(22, 2, 2);
        GridStack(40, name, new() { IntParams = [units] }, 1, running, 0);
        ExecutorActor(1, 0);
        var interval = units * (name == "wf_trg_period_long" ? 5000 : 500);
        var passes = RunRoom(manager, clock, clock.Ms + placedAfterMs, period: 25);
        var placedAt = clock.Ms;
        GridStack(60, name, new() { IntParams = [units] }, 3, placed, 0);
        passes.AddRange(RunRoom(manager, clock, clock.Ms + 3 * interval + 200, period: 25));

        var fromRunning = passes.Where(pass => Touches(pass, running)).Select(pass => pass.At).ToArray();
        var fromPlaced = passes.Where(pass => Touches(pass, placed)).Select(pass => pass.At).ToArray();
        Assert.True(fromPlaced.Length >= 2, $"{fromPlaced.Length} firings of the placed one");
        Assert.All(fromPlaced, at => Assert.Contains(at, fromRunning));
        Assert.InRange(fromPlaced[0] - placedAt, 1, interval);
        Assert.All(fromPlaced.Zip(fromPlaced.Skip(1)), pair => Assert.Equal(interval, pair.Second - pair.First));
    }
}
