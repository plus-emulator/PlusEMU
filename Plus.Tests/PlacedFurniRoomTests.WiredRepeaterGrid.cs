using Xunit;

namespace Plus.Tests;

// Every Repeat of a room counts on the room timer, so one placed later fires on the same ticks as one already running.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_trg_periodically", 2, 4)]
    [InlineData("wf_trg_periodically", 3, 3)]
    [InlineData("wf_trg_period_long", 1, 2)]
    public void ARepeaterPlacedHalfASecondLaterFiresOnTheTicksOfTheOneAlreadyRunning(string name, int units, int firings)
    {
        var (clock, manager) = GridRoom();
        var running = ToggleAt(20, 2, 0);
        var placed = ToggleAt(22, 2, 2);
        GridStack(40, name, new() { IntParams = [units] }, 1, running, 0);
        ExecutorActor(1, 0);
        var passes = RunRoom(manager, clock, clock.Ms + 500, period: 25);
        GridStack(60, name, new() { IntParams = [units] }, 3, placed, 0);
        passes.AddRange(RunRoom(manager, clock, clock.Ms + (firings + 1) * units * (name == "wf_trg_period_long" ? 5000 : 500), period: 25));

        var fromRunning = passes.Where(pass => Touches(pass, running)).Select(pass => pass.At).ToArray();
        var fromPlaced = passes.Where(pass => Touches(pass, placed)).Select(pass => pass.At).ToArray();
        Assert.True(fromRunning.Length >= firings, $"{fromRunning.Length} firings of the running one");
        Assert.True(fromPlaced.Length >= firings - 1, $"{fromPlaced.Length} firings of the placed one");
        Assert.All(fromPlaced, at => Assert.Contains(at, fromRunning));
    }

    // The first Repeat to arm after a room load starts the room timer on its arming tick, so it first fires one interval after that tick.
    [Theory]
    [InlineData("wf_trg_periodically", 2, 1000)]
    [InlineData("wf_trg_period_long", 1, 5000)]
    public void TheFirstFireAfterARoomLoadComesOneIntervalAfterTheArmingTick(string name, int units, int intervalMs)
    {
        var (clock, manager) = GridRoom();
        var running = ToggleAt(20, 2, 0);
        var placed = ToggleAt(22, 2, 2);
        ExecutorActor(1, 0);
        RunRoom(manager, clock, clock.Ms + 500, period: 25);
        var armed = clock.Ms;
        GridStack(40, name, new() { IntParams = [units] }, 1, running, 0);
        var passes = RunRoom(manager, clock, armed + 500, period: 25);
        GridStack(60, name, new() { IntParams = [units] }, 3, placed, 0);
        passes.AddRange(RunRoom(manager, clock, armed + intervalMs + 200, period: 25));

        Assert.Equal([armed + intervalMs], passes.Where(pass => Touches(pass, running)).Select(pass => pass.At).ToArray());
        Assert.Equal([armed + intervalMs], passes.Where(pass => Touches(pass, placed)).Select(pass => pass.At).ToArray());
    }

    // Reset Timers restarts the room timer on the next room tick, and a Repeat placed afterwards counts whole intervals from that tick.
    [Fact]
    public void ARepeatPlacedAfterResetTimersFiresOnTheResetTickPlusWholeIntervals()
    {
        var (clock, manager) = GridRoom();
        var target = ToggleAt(20, 2, 0);
        ExecutorFloor(GridWalkTile, 1, 1);
        var actor = ExecutorActor(1, 0);
        var trigger = WiredBox(30, "wf_trg_walks_on_furni", 3, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger, new() { IntParams = [100], SelectedItems = [GridWalkTile] }, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(trigger));
        var reset = WiredBox(31, "wf_act_reset_timers", 3, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(reset, new(), out config, out error), error);
        reset.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(reset));
        actor.MoveTo(1, 3);
        var passes = new List<GridPass>();

        while (!passes.Any(pass => Touches(pass, reset.Item))) {
            Assert.True(clock.Ms < 1_010_000, "Reset Timers never ran");
            passes.AddRange(RunRoom(manager, clock, clock.Ms + 25, period: 25));
        }

        var resetAt = passes.Single(pass => Touches(pass, reset.Item)).At;
        var resetTick = resetAt - resetAt % 500 + 500;
        passes.AddRange(RunRoom(manager, clock, resetTick + 550, period: 25));
        GridStack(40, "wf_trg_periodically", new() { IntParams = [2] }, 1, target, 0);
        passes.AddRange(RunRoom(manager, clock, resetTick + 3200, period: 25));

        Assert.Equal([resetTick + 1000, resetTick + 2000, resetTick + 3000], passes.Where(pass => Touches(pass, target)).Select(pass => pass.At).ToArray());
    }
}
