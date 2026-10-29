using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A text output add-on that was placed and never saved is ignored: its stack runs every effect, other placeholders are replaced and its own
// placeholder stays as written.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_xtra_text_output_variable")]
    [InlineData("wf_xtra_text_output_username")]
    [InlineData("wf_xtra_text_output_furni_name")]
    public void AnUnsavedTextOutputAddonDoesNotStopItsStack(string unsaved)
    {
        var (wired, actor) = PrepareSameTickVariable();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 7, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 3, 3);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "g[$(good)] e[$(empty)] end" }, 1, 3, 3);
        AddSpeechBox(313, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "other" }, 2, 3, 3);
        var good = WiredBox(314, "wf_xtra_text_output_variable", 3, 3);
        good.Item.GetZ = 3;
        WiredNativeTestSupport.Install(good, WiredNativeEditorProjection.DefaultNative(good.Descriptor) with
        {
            OwnedIntParams = ImmutableArray.Create(0, 1, 0),
            VariableIds = ["user:993"],
            Text = "good"
        });
        Assert.True(_room.GetWired().AddBox(good));
        var empty = WiredBox(315, unsaved, 3, 3);
        empty.Item.GetZ = 4;
        Assert.True(_room.GetWired().AddBox(empty));
        wired.FlushFlashes();
        actor.OnChat(0, "vg1", false);
        wired.OnFastCycle();
        _client.Packets.Clear();

        actor.OnChat(0, "go", false);
        wired.OnFastCycle();

        var messages = SpeechMessages(ServerPacketHeader.ChatComposer);
        Assert.Contains("g[7] e[$(empty)] end", messages);
        Assert.Contains("other", messages);
    }
}
