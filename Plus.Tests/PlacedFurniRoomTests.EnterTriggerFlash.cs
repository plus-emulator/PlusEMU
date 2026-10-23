using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // An Enter Room stack runs inside the room entry; its flash comes after the labels and before the controller packets.
    [Fact]
    public void EnterRoomStackFlashesInsideTheRoomEntryBeforeTheControllerPackets()
    {
        var trigger = WiredBox(600, "wf_trg_enter_room", 2, 2);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_enter_room"));
        Assert.True(_room.GetWired().AddBox(trigger));
        var effect = WiredBox(601, "wf_act_show_message", 2, 2);
        ModernWiredRuntimeTests.LoadStoredRuntime(Assert.IsType<WiredModernAction>(effect), "wf_act_show_message",
            new WiredConfiguration { IntParams = [0, 1, 34, -1], Text = "hello" });
        Assert.True(_room.GetWired().AddBox(effect));

        EnterForNotice();

        var sent = _client.Sent.ToList();
        var flash = sent.FindIndex(id => id is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.ObjectUpdateComposer);
        Assert.True(flash >= 0, "the Enter Room stack did not flash during the entry");
        Assert.True(flash > sent.IndexOf(ServerPacketHeader.WiredLabelsComposer));
        Assert.True(flash < sent.IndexOf(ServerPacketHeader.YouAreControllerComposer));
    }
}
