using System.Text.RegularExpressions;
using Xunit;

namespace Plus.Tests;

// The snapshot text an opened snapshot editor shows. A field its option leaves unticked is written as N. Observed on AIR
// WIN63-202610201032-107964540.
public sealed class WiredSnapshotTextTests
{
    public static TheoryData<string> Conditions => new() { "wf_cnd_match_snapshot", "wf_cnd_not_match_snap" };

    // The text of a save that keeps every field: the id, the state, the rotation, x, y and the altitude.
    private static string[] AllFields(string name)
    {
        using var all = new WiredRawSave(name);
        var id = all.Furni();
        var text = all.Save(ints: [1, 1, 1, 1], picks: [id]).Text;
        Assert.Matches(new Regex(@"^\d+(,[^,N]+){5}$"), text);

        return text.Split(',');
    }

    [Theory]
    [MemberData(nameof(Conditions))]
    public void AFieldItsOptionLeavesUntickedIsWrittenAsN(string name)
    {
        var reference = AllFields(name);

        foreach (var (ints, kept) in new[]
        {
            (new[] { 0, 0, 0, 0 }, new bool[] { false, false, false, false, false }),
            (new[] { 1, 0, 0, 0 }, new[] { true, false, false, false, false }),
            (new[] { 0, 1, 0, 0 }, new[] { false, true, false, false, false }),
            (new[] { 0, 0, 1, 0 }, new[] { false, false, true, true, false }),
            (new[] { 0, 0, 0, 1 }, new[] { false, false, false, false, true }),
            (new[] { 1, 1, 1, 1 }, new[] { true, true, true, true, true }),
            (new[] { 2, 0, 0, 0 }, new[] { false, false, false, false, false })
        }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: ints, picks: [save.Furni()]);
            Assert.True(result.Accepted, result.ToString());
            var parts = result.Text.Split(',');

            Assert.Equal(6, parts.Length);

            for (var part = 0; part < 5; part++) {
                Assert.Equal(kept[part] ? reference[part + 1] : "N", parts[part + 1]);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Conditions))]
    public void EveryEntryOfSeveralPicksIsWrittenWithTheSameNs(string name)
    {
        using var save = new WiredRawSave(name);
        var result = save.Save(ints: [1, 0, 0, 0], picks: [save.Furni(), save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Matches(new Regex(@"^\d+,[^,N]+,N,N,N,N;\d+,[^,N]+,N,N,N,N$"), result.Text);
    }

    [Theory]
    [InlineData(0, @"^\d+,N,N,N,N,N$")]
    [InlineData(1, @"^\d+,[^,N]+,N,N,N,N$")]
    [InlineData(2, @"^\d+,N,N,N,N,N$")]
    public void StuffStateWritesItsSnapshotWithNForEveryFieldButTheState(int mode, string pattern)
    {
        using var save = new WiredRawSave("wf_trg_stuff_state");
        var result = save.Save(ints: [mode], picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Matches(new Regex(pattern), result.Text);
    }

    // Observed on AIR WIN63-202610201032-107964540: a save without ints stores the frame default, but its snapshot keeps no state.
    [Fact]
    public void StuffStateSavedWithoutIntsDropsTheSnapshotState()
    {
        using var save = new WiredRawSave("wf_trg_stuff_state");
        var result = save.Save(ints: [], picks: [save.Furni()]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([1], result.Ints);
        Assert.Matches(new Regex(@"^\d+,N,N,N,N,N$"), result.Text);
        Assert.Matches(new Regex(@"^\d+,[^,N]+,N,N,N,N$"), save.Save(ints: [1], picks: [save.Furni()]).Text);
    }

    [Fact]
    public void StuffStateWithNoPickHasNoSnapshotText()
    {
        using var save = new WiredRawSave("wf_trg_stuff_state");

        Assert.Equal("", save.Save(ints: [1]).Text);
    }
}
