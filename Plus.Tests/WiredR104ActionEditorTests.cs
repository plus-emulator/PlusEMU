using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Action editor saves as Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR104ActionEditorTests
{
    private static Result Saved(string name, int[] ints)
    {
        using var world = new G1World();
        var result = Save(world, name, ints);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints)}]: {result}");

        return result;
    }

    [Fact]
    public void MatchToSnapshotSwitchesAnOptionOnOnlyForTheExactValueOne()
    {
        const string name = "wf_act_match_to_sshot";

        Assert.Equal([1, 1, 1, 1], Saved(name, [1, 1, 1, 1]).Ints[..4]);
        Assert.Equal([0, 0, 0, 0], Saved(name, [0, 0, 0, 0]).Ints[..4]);

        foreach (var option in new[] { 2, -1, 100, int.MinValue }) {
            Assert.Equal([0, 0, 0, 0], Saved(name, [option, option, option, option]).Ints[..4]);
        }

        Assert.Equal([1, 0, 0, 0], Saved(name, [1]).Ints[..4]);
        Assert.Equal([0, 0, 0, 0], Saved(name, []).Ints[..4]);
        Assert.Equal([1, 1, 1, 1], Saved(name, [1, 1, 1, 1, 1]).Ints[..4]);
        Assert.Equal([1, 0, 0, 0], Saved(name, [1, 0, 0, 0, 0, 7]).Ints[..4]);
    }
}
