using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A stored comparison of 3 passes when the furni altitude is at most the value.
    [Theory]
    [InlineData(1.5, 100, false)]
    [InlineData(1.5, 150, true)]
    [InlineData(1.5, 200, true)]
    [InlineData(0.0, 0, true)]
    [InlineData(0.0, 100, true)]
    public async Task HasAltitudeComparisonThreeSavedByPacketPassesWhenTheFurniIsAtMostTheValue(double height, int value, bool passes)
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        var block = Furni(620, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, block, 2, 2, 0, true, false, false));
        block.GetZ = height;
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var condition = WiredBox(302, "wf_cnd_has_altitude", 1, 1);
        condition.Item.GetZ = 1;
        Assert.True(wired.AddBox(condition));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "at most" }, 2);
        var fresh = WiredNativeEditorProjection.DefaultNative(condition.Descriptor);
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());

        await new SaveWiredConditionConfigEvent(service).Parse(_client, ClientPacket([302, 2, value, 3, "", 1, 620, 0,
            fresh.FurniSourceTypes.Length, .. fresh.FurniSourceTypes.Cast<object>(), fresh.UserSourceTypes.Length, .. fresh.UserSourceTypes.Cast<object>(), 0, 0]));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([passes ? "at most" : "pulse"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }
}
