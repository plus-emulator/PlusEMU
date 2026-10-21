using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Trigger editor saves as Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR104TriggerEditorTests
{
    private static Result Saved(string name, int[]? ints = null, string? text = null)
    {
        using var world = new G1World();
        var result = Save(world, name, ints, text: text);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints ?? [])}]: {result}");

        return result;
    }

    [Fact]
    public void FurniStateChangedOnlyTheExactValueOneIsTheCurrentStateMode()
    {
        const string name = "wf_trg_stuff_state";

        Assert.Equal([1], Saved(name, [1]).Ints);
        Assert.Equal([0], Saved(name, [0]).Ints);

        foreach (var mode in new[] { 2, 3, -1, 100, int.MaxValue }) {
            Assert.Equal([0], Saved(name, [mode]).Ints);
        }

        Assert.Equal([1], Saved(name, []).Ints);
    }

    [Fact]
    public void ClickUserStoresAnyNonZeroFlagAsOne()
    {
        const string name = "wf_trg_click_user";

        Assert.Equal([0, 0], Saved(name, [0, 0]).Ints);
        Assert.Equal([1, 1], Saved(name, [1, 1]).Ints);
        Assert.Equal([1, 1], Saved(name, [2, -1]).Ints);
        Assert.Equal([1, 1], Saved(name, [-1, 5]).Ints);
        Assert.Equal([1, 0], Saved(name, [1]).Ints);
        Assert.Equal([1, 0], Saved(name, []).Ints);
        Assert.Equal([1, 0], Saved(name, [1, 0, 3]).Ints);
    }

    [Fact]
    public void ClockCounterClampsSecondsMinutesAndTheHalfSecond()
    {
        const string name = "wf_trg_clock_counter";

        Assert.Equal([2, 0, 0], Saved(name, [2, 0, 0]).Ints);
        Assert.Equal([59, 0, 0], Saved(name, [59, 0, 0]).Ints);

        foreach (var seconds in new[] { 60, int.MaxValue }) {
            Assert.Equal([59, 0, 0], Saved(name, [seconds, 0, 0]).Ints);
        }

        Assert.Equal([0, 0, 0], Saved(name, [-1, 0, 0]).Ints);
        Assert.Equal([0, 99, 0], Saved(name, [0, 99, 0]).Ints);
        Assert.Equal([0, 99, 0], Saved(name, [0, 100, 0]).Ints);
        Assert.Equal([0, 0, 0], Saved(name, [0, -1, 0]).Ints);
        Assert.Equal([0, 0, 1], Saved(name, [0, 0, 1]).Ints);
        Assert.Equal([0, 0, 1], Saved(name, [0, 0, 2]).Ints);
        Assert.Equal([0, 0, 0], Saved(name, [0, 0, -1]).Ints);
        Assert.Equal([5, 0, 0], Saved(name, [5]).Ints);
        Assert.Equal([1, 2, 0], Saved(name, [1, 2, 0, 5]).Ints);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(100)]
    public void BotReachesAvatarKeepsABotNameOfAnyLength(int length)
    {
        var name = new string('a', length);

        Assert.Equal(name, Saved("wf_trg_bot_reached_avtr", text: name).Text);
        Assert.Equal("<b>x</b>", Saved("wf_trg_bot_reached_avtr", text: "<b>x</b>").Text);
    }
}
