using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private string[] ShowMessageOutput(string text, bool withUsernameAddon)
    {
        var (_, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "pulse" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 0, 202, -1], Text = text }, 1);

        if (withUsernameAddon) {
            AddSpeechBox(303, "wf_xtra_text_output_username", new() { IntParams = [1, 0], Text = "clickprobeuser\t, " }, 2);
        }

        _client.Packets.Clear();
        actor.OnChat(0, "pulse", false);

        return SpeechMessages(Plus.Communication.Packets.Outgoing.ServerPacketHeader.WhisperComposer);
    }

    [Theory]
    [InlineData("wiredprobeusernameqzvkxwmb B<$(clickprobeuser)>E", "wiredprobeusernameqzvkxwmb BE")]
    [InlineData("wiredprobeusernameqzvkxwmb $(clickprobeuser)", "wiredprobeusernameqzvkxwmb $(clickprobeuser)")]
    public void ShowMessageRemovesAngleBracketSegmentsAtOutputAndKeepsUnknownPlaceholdersLiteral(string saved, string delivered) =>
        // hotel-text-markup-fact-r64 / hotel-unresolved-placeholder-fact-r65
        Assert.Equal([delivered], ShowMessageOutput(saved, false));

    [Fact]
    public void ShowMessageSubstitutesUsernamePlaceholderWithTriggeringUserDisplayName()
    {
        // hotel-username-placeholder-fact-r68
        var output = ShowMessageOutput("wiredprobeusernameqzvkxwmb $(clickprobeuser)", true);
        var message = Assert.Single(output);
        Assert.StartsWith("wiredprobeusernameqzvkxwmb ", message);
        Assert.DoesNotContain("$(", message);
        Assert.Equal("wiredprobeusernameqzvkxwmb " + _client.GetHabbo().Username, message);
    }

    [Theory]
    [InlineData("a<b>c", "ac")]
    [InlineData("<x>", "")]
    [InlineData("a<<b>>c", "ac")]
    [InlineData("a < b", "a < b")]
    [InlineData("[b]bold[/b] $(x)", "[b]bold[/b] $(x)")]
    public void AngleSegmentStripping(string input, string expected) =>
        Assert.Equal(expected, WiredModernAction.StripAngleSegments(input));
}
