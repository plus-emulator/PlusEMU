using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A "user says keyword" stack offers $(trigger_text) to its message editors and fills it with what was said.
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

    // The room sends one ObjectsDataUpdate every 500 ms with every furni whose state changed.
    [Fact]
    public void BoxFlashesGoOutTogetherInOneDataUpdate()
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        var trigger = AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "pulse" }, 0);
        var show = AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 1);
        wired.FlushFlashes();
        _client.Packets.Clear();

        wired.OnEvent(trigger.Item);
        wired.OnEvent(show.Item);
        Assert.Empty(_client.Packets.Where(packet => packet.Header is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.ObjectUpdateComposer));
        wired.FlushFlashes();

        var batch = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        var input = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = batch.Body };
        Assert.Equal(2, input.ReadInt());
        Assert.Empty(_client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer));
    }

    // A box flash goes out with the chat that fired it. A second stack fired 200 ms later flashes at once, not on a shared 500 ms room interval.
    [Fact]
    public void EachFiringFlashesInItsOwnPass()
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "one" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 1);
        AddSpeechBox(303, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "two" }, 0, x: 2);
        AddSpeechBox(304, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "B" }, 1, x: 2);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "one", false);
        wired.OnFastCycle();
        actor.OnChat(0, "two", false);
        wired.OnFastCycle();

        Assert.Equal(2, _client.Packets.Count(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer));
    }

    // A hidden chat is echoed to its speaker after the stack's flash and message.
    [Fact]
    public void HiddenSpeechEchoFollowsTheFlashAndTheStackMessage()
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [0, 1, 1], Text = "one" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 202, -1], Text = "A" }, 1);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "one", false);

        Assert.Equal([ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.ChatComposer, ServerPacketHeader.WhisperComposer],
            _client.Packets.Select(packet => packet.Header));
    }

    // Speech runs its stacks outside the room's passes; the flash goes out with the chat, not on the next pass.
    [Fact]
    public void SpeechFlashesGoOutWithTheChat()
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "one" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 1);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "one", false);

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        // The flash comes before the message the stack sends and before the chat.
        Assert.Equal(ServerPacketHeader.ObjectsDataUpdateComposer, _client.Packets[0].Header);
    }
}
