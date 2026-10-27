using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// A called stack does not inherit the caller's Selector result. It reads its own selectors, and it keeps the triggering user.
public partial class PlacedFurniRoomTests
{
    private const int SelectorSource = 200;

    // The callee stack sits on tile (2, 2); its Show Message is box 312. The caller sits on tile (1, 1).
    [Theory]
    [InlineData(SelectorSource, null, false)]
    [InlineData(0, null, true)]
    [InlineData(SelectorSource, "wf_slc_users_bytype:3", false)]
    [InlineData(SelectorSource, "wf_slc_users_bytype:1", true)]
    public void ACalledStackReadsItsOwnSelectorsAndTheTriggeringUserButNotTheCallersSelection(int source, string? ownSelector, bool delivered)
    {
        var (wired, owner) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_slc_users_area", new() { IntParams = [0, 0, 5, 5, 0, 0] }, 1);
        AddSpeechBox(303, "wf_act_call_stacks", new() { IntParams = [100], SelectedItems = [312] }, 2);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "never" }, 0, 2, 2);
        var next = 0;

        if (ownSelector != null) {
            var parts = ownSelector.Split(':');
            AddSpeechBox(320, parts[0], new() { IntParams = [int.Parse(parts[1]), 0, 0] }, 1, 2, 2);
            next = 1;
        }

        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [source, 0, 34, -1], Text = "callee" }, 1 + next, 2, 2);
        _client.Packets.Clear();

        owner.OnChat(0, "go", false);
        wired.OnFastCycle();

        Assert.Equal(delivered, ChatTexts(_client, ServerPacketHeader.WhisperComposer).Contains("callee"));
    }

    [Fact]
    public void ACalledStackWithoutSelectionGetsNothingWhenTheCallerSelectedNobody()
    {
        var (wired, owner) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_slc_users_bytype", new() { IntParams = [3, 0, 0] }, 1);
        AddSpeechBox(303, "wf_act_call_stacks", new() { IntParams = [100], SelectedItems = [312] }, 2);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "never" }, 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [SelectorSource, 0, 34, -1], Text = "callee" }, 1, 2, 2);
        _client.Packets.Clear();

        owner.OnChat(0, "go", false);
        wired.OnFastCycle();

        Assert.DoesNotContain("callee", ChatTexts(_client, ServerPacketHeader.WhisperComposer));
    }
}
