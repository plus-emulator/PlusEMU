using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A "user says keyword" stack offers $(trigger_text) to its message editors and fills it with what was said
    // (official capture of an editor over such a stack: placeholderNames ["trigger_text"], capturerNames []).
    [Fact]
    public void UserSaysKeywordStacksOfferAndFillTriggerText()
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "pulse" }, 0);
        var show = AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "said $(trigger_text)" }, 1);

        var placeholders = Assert.IsType<Plus.HabboHotel.Items.Wired.Runtime.WiredTextPlaceholders>(WiredEditorSnapshot.Capture(show).TextPlaceholders);
        Assert.Equal(["trigger_text"], placeholders.PlaceholderNames.ToArray());
        Assert.Empty(placeholders.CapturerNames);

        _client.Packets.Clear();
        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Contains("said pulse", SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    [Fact]
    public void OnlyMessageEditorsCarryTheTextPlaceholderContext()
    {
        PrepareSpeech(new SpeechClock());
        var clock = AddSpeechBox(301, "wf_act_control_clock", new() { IntParams = [0, 100] }, 0);

        Assert.Null(WiredEditorSnapshot.Capture(clock).TextPlaceholders);
    }

    // Room 79874981 sends one ObjectsDataUpdate every 500 ms with every furni whose state changed (WIN63-202610121709).
    [Fact]
    public void BoxFlashesGoOutTogetherInOneDataUpdate()
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        var trigger = AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "pulse" }, 0);
        var show = AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 1);
        wired.FlushFlashes(force: true);
        _client.Packets.Clear();

        wired.OnEvent(trigger.Item);
        wired.OnEvent(show.Item);
        Assert.Empty(_client.Packets.Where(packet => packet.Header is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.ObjectUpdateComposer));
        wired.FlushFlashes(force: true);

        var batch = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        var input = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = batch.Body };
        Assert.Equal(2, input.ReadInt());
        Assert.Empty(_client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer));
    }
}
