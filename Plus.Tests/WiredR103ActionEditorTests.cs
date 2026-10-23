using Xunit;
using static Plus.Tests.WiredR103EditorSave;

namespace Plus.Tests;

// Move Furni As Group and Negative Send Signal saves as Habbo stores them.
public sealed class WiredR103ActionEditorTests
{
    [Fact]
    public void MoveAsGroupKeepsTargetFlagZeroAndOneAndEveryOtherValueBecomesOne()
    {
        Assert.Equal([0, 0, 0], Saved("wf_act_move_furni_as_group", [0, 0, 0]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_act_move_furni_as_group", [1, 0, 0]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_act_move_furni_as_group", [2, 0, 0]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_act_move_furni_as_group", [-1, 0, 0]).Ints);
    }

    [Fact]
    public void MoveAsGroupClampsEachOffsetToMinus64To64()
    {
        foreach (var offset in new[] { 65, 1000, int.MaxValue }) {
            Assert.Equal([1, 64, 64], Saved("wf_act_move_furni_as_group", [1, offset, offset]).Ints);
        }

        foreach (var offset in new[] { -65, -1000, int.MinValue }) {
            Assert.Equal([1, -64, -64], Saved("wf_act_move_furni_as_group", [1, offset, offset]).Ints);
        }

        Assert.Equal([1, 64, -64], Saved("wf_act_move_furni_as_group", [1, 64, -64]).Ints);
    }

    [Fact]
    public void MoveAsGroupMissingIntsTakeTheFrameDefaultAndExtraIntsAreDropped()
    {
        Assert.Equal([0, 0, 0], Saved("wf_act_move_furni_as_group", [0]).Ints);
        Assert.Equal([0, 3, 0], Saved("wf_act_move_furni_as_group", [0, 3]).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_act_move_furni_as_group", []).Ints);
        Assert.Equal([1, 0, 0], Saved("wf_act_move_furni_as_group", [1, 0, 0, 9]).Ints);
    }

    [Fact]
    public void SendSignalFlagsCountOnlyWhenTheyAreExactlyOne()
    {
        const string name = "wf_act_neg_send_signal";

        Assert.Equal([1, 1], Saved(name, [1, 1]).Ints);

        foreach (var flag in new[] { 2, -1, 5 }) {
            Assert.Equal([0, 0], Saved(name, [flag, flag]).Ints);
        }

        Assert.Equal([1, 0], Saved(name, [1]).Ints);
        Assert.Equal([0, 0], Saved(name, []).Ints);
        Assert.Equal([0, 0], Saved(name, [0, 0, 1]).Ints);
    }
}
