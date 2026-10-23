using Xunit;

namespace Plus.Tests;

public class WiredSshotEditorIntsTests
{
    // The editor (code 3) reads four option ints and returns exactly four, so the save check must accept that echo.
    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 1, 0)]
    [InlineData(1, 1, 1, 1)]
    public void FourIntEchoOfTheSnapshotEditorSaves(int state, int rotation, int position, int altitude)
    {
        Assert.True(WiredOctoberEditorParityTests.TryCompile("wf_act_match_to_sshot",
            native => native with { OwnedIntParams = [state, rotation, position, altitude] }));
    }

    [Fact]
    public void SavedSnapshotKeepsItsFiveStoredInts()
    {
        var runtime = WiredOctoberEditorParityTests.Compile("wf_act_match_to_sshot",
            native => native with { OwnedIntParams = [0, 0, 0, 0] });

        Assert.Equal(5, runtime.IntParams.Length);
    }
}
