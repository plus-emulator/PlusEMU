using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Variables.Fx;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A say whose stack gives a variable shown by a Variable FX box sends the FX status in the same tick, before the packet that flashes the stack's boxes.
    [Fact]
    public void VariableFxStatusGoesOutBeforeTheFlashOfTheSayThatGaveTheVariable()
    {
        var (wired, actor) = PrepareSameTickVariable();
        typeof(WiredRoomVariables).GetField("<Fx>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, new WiredVariableFxTracker(wired.Variables.Module));
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "fx1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddDefinitionBox(SameTickVariableId, "wf_var_user", 0, 2, 2);
        AddDefinitionBox(320, "wf_xtra_var_fx_number", 1, 2, 2);
        wired.SnapshotEnqueued(_client, _room.GetRoomItemHandler().GetFloor, [actor]);
        wired.OnFastCycle();
        _client.Packets.Clear();

        actor.OnChat(0, "fx1", false);

        var order = _client.Packets.Select(packet => packet.Header)
            .Where(header => header is ServerPacketHeader.WiredVariableFxStatusComposer or ServerPacketHeader.ObjectsDataUpdateComposer).ToArray();
        Assert.Equal([ServerPacketHeader.WiredVariableFxStatusComposer, ServerPacketHeader.ObjectsDataUpdateComposer], order);
    }

    private void AddDefinitionBox(uint id, string name, double height, int x, int y)
    {
        var box = WiredBox(id, name, x, y);
        box.Item.GetZ = height;
        WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { Text = name == "wf_var_user" ? "score" : "" });
        Assert.True(_room.GetWired().AddBox(box));
        _room.GetWired().Variables.ConfigurationSaved(box);
    }
}
