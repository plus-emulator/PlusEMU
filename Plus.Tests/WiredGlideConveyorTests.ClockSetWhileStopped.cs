using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private static Fixture StoppedSetFixture()
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
            (23, 5, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cstop")),
            (24, 5, 12, 0.65, 0, "wf_act_control_clock", Config([1, 100], "", CounterId)),
            (25, 6, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cset")),
            (26, 6, 12, 0.65, 0, "wf_act_adjust_clock", Config([2, 100, 0, 4], "", CounterId))
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);

        return f;
    }

    // Observed on AIR WIN63-202610201032-107964540: a counter that ran and was stopped starts again from 0, also when a Set Clock gave it a
    // value while it was stopped. The Set shows its value at once, and the Start writes 0 in a batch of its own.
    [Fact]
    public void WiredStartAfterAStopAndASetRestartsTheCounterFromZero()
    {
        var f = StoppedSetFixture();
        var rider = f.Walker(1, 4, 10, 4, 11);
        var counter = f.Items[CounterId];

        f.Owned(() => rider.OnChat(0, "cstart", false));
        f.Advance(3000);
        f.Owned(() => rider.OnChat(0, "cstop", false));
        f.Advance(1000);
        var frames = CaptureFrames(rider);
        f.Owned(() => rider.OnChat(0, "cset", false));
        f.Advance(100);
        Assert.Equal("2", counter.LegacyDataString);
        f.Advance(1000);
        f.Owned(() => rider.OnChat(0, "cstart", false));
        f.Advance(100);

        var batches = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(CounterEntries)
            .Where(entries => entries.Length > 0).ToArray();
        Assert.Equal(["2", "0"], batches.Select(entries => Assert.Single(entries).Text));
        Assert.Equal("0", counter.LegacyDataString);
        f.Advance(2500);
        Assert.Equal("2", counter.LegacyDataString);
    }

    // Observed on AIR WIN63-202610151745-318700621: a counter that has not run yet keeps the value a Set Clock gave it when it starts.
    [Fact]
    public void WiredStartOfACounterThatNeverRanKeepsTheValueOfItsSet()
    {
        var f = StoppedSetFixture();
        var rider = f.Walker(1, 4, 10, 4, 11);
        var counter = f.Items[CounterId];

        f.Owned(() => rider.OnChat(0, "cset", false));
        f.Advance(1000);
        Assert.Equal("2", counter.LegacyDataString);
        f.Owned(() => rider.OnChat(0, "cstart", false));
        f.Advance(100);
        Assert.Equal("2", counter.LegacyDataString);
        f.Advance(1500);
        Assert.Equal("3", counter.LegacyDataString);
    }
}
