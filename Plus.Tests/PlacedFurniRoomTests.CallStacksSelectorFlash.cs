using Xunit;

namespace Plus.Tests;

// A called stack flashes its actions. Its own selector does not flash, whether it selects somebody or nobody. The caller's selector flashes.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_slc_users_bytype", 1)]
    [InlineData("wf_slc_users_bytype", 3)]
    public void ACalledStackFlashesItsActionsButNotItsOwnSelector(string selector, int userType)
    {
        var (wired, owner) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_slc_users_area", new() { IntParams = [0, 0, 5, 5, 0, 0] }, 1);
        AddSpeechBox(303, "wf_act_call_stacks", new() { IntParams = [100], SelectedItems = [311] }, 2);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "never" }, 0, 2, 2);
        AddSpeechBox(320, selector, new() { IntParams = [userType, 0, 0] }, 1, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [SelectorSource, 0, 34, -1], Text = "callee" }, 2, 2, 2);
        wired.FlushFlashes();
        _client.Packets.Clear();

        owner.OnChat(0, "go", false);
        wired.OnFastCycle();
        wired.FlushFlashes();

        var flashed = DataUpdates(_client).SelectMany(batch => batch.Select(entry => entry.Id)).ToHashSet();
        Assert.Contains(312u, flashed);
        Assert.Contains(302u, flashed);
        Assert.DoesNotContain(320u, flashed);
        Assert.DoesNotContain(311u, flashed);
    }
}
