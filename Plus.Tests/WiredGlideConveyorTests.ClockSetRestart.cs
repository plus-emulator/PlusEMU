using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Set Clock on a running counter restarts its tick. The next second shows 1000 ms after the Set, however the Set falls on
    // the 500 ms room grid. A step that was already due is not taken at once.
    [Theory]
    [InlineData(2100)]
    [InlineData(2250)]
    [InlineData(2450)]
    [InlineData(2500)]
    [InlineData(2550)]
    public void WiredSetOfARunningCounterShowsTheNextSecondOneSecondLater(int setAt)
    {
        string Config(int[] parameters, string text = "", params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            Text = text,
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, "wf_upcounter1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cstart")),
            (22, 4, 12, 0.65, 0, "wf_act_control_clock", Config([0, 100], "", CounterId)),
            (23, 5, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cset")),
            (24, 5, 12, 0.65, 0, "wf_act_adjust_clock", Config([2, 100, 0, 2], "", CounterId))
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var counter = f.Items[CounterId];

        f.Owned(() => rider.OnChat(0, "cstart", false));
        f.Advance(setAt);
        f.Owned(() => rider.OnChat(0, "cset", false));
        f.Advance(50);
        Assert.Equal("1", counter.LegacyDataString);
        f.Advance(850);
        Assert.Equal("1", counter.LegacyDataString);
        f.Advance(200);
        Assert.Equal("2", counter.LegacyDataString);
        f.Advance(800);
        Assert.Equal("2", counter.LegacyDataString);
        f.Advance(200);
        Assert.Equal("3", counter.LegacyDataString);
    }
}
