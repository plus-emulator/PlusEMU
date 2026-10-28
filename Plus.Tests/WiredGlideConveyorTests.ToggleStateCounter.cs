using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Toggle State starts a game up counter and counts it up every second; a later firing does not restart or stop it.
    [Fact]
    public void ToggleStateStartsAGameUpCounterAndLaterFiringsLeaveItCounting()
    {
        var f = ClockFixture("wf_game_upcounter1", "wf_act_toggle_state", [0]);
        f.Items[CounterId].LegacyDataString = "0";
        var rider = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(rider);

        f.Advance(9000);

        var shown = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer).Select(ReadSingle)
            .Where(entry => entry.Id == CounterId).Select(entry => entry.Text).ToArray();
        Assert.True(Counters(f).IsRunning(f.Items[CounterId]));
        Assert.True(shown.Length >= 6, string.Join(",", shown));
        Assert.Equal(Enumerable.Range(1, shown.Length).Select(n => n.ToString()), shown);
    }
}
