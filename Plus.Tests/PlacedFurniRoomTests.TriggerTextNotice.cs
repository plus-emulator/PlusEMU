using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Polls;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610131318-860371703: a box text with $(trigger_text) makes room entry send NotificationDialog
    // "wired.error" (2356) to the entrant, owner or visitor, right after the room-enter packet (1647).
    [Theory]
    [InlineData("wf_act_show_message", true)]
    [InlineData("wf_act_bot_talk", true)]
    [InlineData("wf_act_show_message", false)]
    [InlineData("wf_act_bot_talk", false)]
    public void TriggerTextBoxSendsTheCaptureNoticeRightAfterRoomEnter(string name, bool owner)
    {
        AddTextBox(name, name == "wf_act_bot_talk" ? "Bobby\tyou said $(trigger_text)" : "you said $(trigger_text)");

        if (!owner) {
            _room.OwnerId = 99;
        }

        EnterForNotice();

        var tail = _client.Sent.SkipWhile(id => id != ServerPacketHeader.RoomEntryInfoComposer).Take(3).ToArray();
        Assert.Equal([ServerPacketHeader.RoomEntryInfoComposer, ServerPacketHeader.RoomNotificationComposer, ServerPacketHeader.WiredLabelsComposer], tail);
        var notice = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.RoomNotificationComposer).Body };
        Assert.Equal("wired.error", notice.ReadString());
        Assert.Equal(2, notice.ReadInt());
        Assert.Equal("image", notice.ReadString());
        Assert.Equal("${image.library.url}album1584/WIRD1.png", notice.ReadString());
        Assert.Equal("message", notice.ReadString());
        Assert.Equal("${room.error.wired.trigger_text_capture}", notice.ReadString());
        Assert.False(notice.HasDataRemaining());
    }

    [Theory]
    [InlineData("wf_act_show_message", "plain text")]
    [InlineData("wf_act_bot_talk", "Bobby\tplain text")]
    public void BoxTextWithoutTriggerTextSendsNoCaptureNotice(string name, string text)
    {
        AddTextBox(name, text);

        EnterForNotice();

        Assert.Contains(ServerPacketHeader.RoomEntryInfoComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.RoomNotificationComposer, _client.Sent);
    }

    private void AddTextBox(string name, string text)
    {
        var box = WiredBox(500, name, 1, 1);
        var configuration = name == "wf_act_bot_talk"
            ? new WiredConfiguration { IntParams = [0, 100, 0, 0], Text = text }
            : new WiredConfiguration { IntParams = [0, 1, 34, -1], Text = text };
        ModernWiredRuntimeTests.LoadStoredRuntime(Assert.IsType<WiredModernAction>(box), name, configuration);
        Assert.True(_room.GetWired().AddBox(box));
    }

    private void EnterForNotice()
    {
        _client.GetHabbo().HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        _client.Packets.Clear();
        var quests = Proxy<IQuestManager>((method, _) => throw new InvalidOperationException(method));
        var polls = Proxy<IRoomPollService>((method, _) => method == nameof(IRoomPollService.Offer) ? null : throw new InvalidOperationException(method));
        new RoomEntryService(quests, TimeProvider.System, polls, EntryWordQuiz).Enter(_client);
    }
}
