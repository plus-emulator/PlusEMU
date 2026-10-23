using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540 (3 of 3 runs, clicks one second apart at three different phases): twelve stacks that
    // answer one click with one Show Message each let ten lines out per click, at most ten in any five seconds (the first ten at +0 s,
    // the next at +5 s ...), and forty in all. After the fourth batch nothing comes out until thirty seconds after the first batch,
    // when ten come out again.
    [Fact]
    public void ShowMessageAllowsFortyLinesInAnyThirtySeconds()
    {
        const int stacks = 12;
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)> { (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null) };

        for (var stack = 0; stack < stacks; stack++) {
            var id = ClickTrigger + (uint)(10 * stack);
            layout.Add((id, 4 + stack % 6, 12 + stack / 6, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)));
            layout.Add((id + 1, 4 + stack % 6, 12 + stack / 6, 0.65, 0, "wf_act_show_message", ShownMessage("shown")));
        }

        var f = new Fixture([.. layout], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        var subject = f.Items[ObservedSubjectId];
        var frames = CaptureFlashFrames(rider);
        var perClick = new List<int>();

        for (var click = 0; click < 40; click++) {
            frames.Clear();
            ObservedClickSubject(f, rider, subject);
            f.Advance(1000);
            perClick.Add(frames.Count(frame => frame.Header == ServerPacketHeader.WhisperComposer));
        }

        Assert.All(perClick.Take(5), lines => Assert.InRange(lines, 0, 10));
        Assert.Equal(10, perClick[0]);
        Assert.Equal(40, perClick.Take(30).Sum());
        Assert.Equal(10, perClick[30]);
        Assert.Equal(0, perClick.Skip(16).Take(14).Sum());
        Assert.Equal(10, perClick[35]);
    }
}
