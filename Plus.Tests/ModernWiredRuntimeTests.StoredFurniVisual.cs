using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// The stricter Set Furni Highlight and Set Furni Visibility rules apply to a new save only. A box saved under the older, wider
// rules must still load with the room and run.
public partial class ModernWiredRuntimeTests
{
    // [audience, type, strength, colour, animation, easing, target users]
    [Theory]
    [InlineData(2, 50, 0)]
    [InlineData(3, 100, 0xFFFFFF)]
    [InlineData(1, 100, 0x123456)]
    public void AStoredHighlightFromOlderRulesStillLoadsAndRuns(int type, int strength, int color)
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        var row = StoredVisualRow("wf_act_set_highlight", [0, type, strength, color, 0, 2, 0]);

        var box = Assert.IsType<WiredModernAction>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(110, "wf_act_set_highlight"), 0, 0),
            Descriptor("wf_act_set_highlight")));
        box.ApplyConfiguration(Assert.IsType<WiredConfiguration>(WiredConfigurationStore.DecodeRow(110, "wf_act_set_highlight", row)));
        Assert.True(f.Engine.Add(box));
        var sent = Capture(f.Client);

        f.Fire(land: false);

        Assert.NotNull(Reply(sent, ServerPacketHeader.WiredFurniHighlightComposer));
    }

    [Fact]
    public void AStoredVisibilityOrHighlightWithAnOldLongDelayStillLoads()
    {
        Assert.NotNull(WiredConfigurationStore.DecodeRow(1, "wf_act_set_highlight", StoredVisualRow("wf_act_set_highlight", [0, 1, 100, 0xFFB700, 0, 0, 0], delay: 200)));
        Assert.NotNull(WiredConfigurationStore.DecodeRow(1, "wf_act_set_visibility", StoredVisualRow("wf_act_set_visibility", [0, 100, 0, 0, 0], delay: 200)));
    }

    private static string StoredVisualRow(string name, int[] ints, int delay = 0) => JsonSerializer.Serialize(
        WiredNativeEditorProjection.DefaultNative(Descriptor(name)) with
        {
            OwnedIntParams = [.. ints],
            PrimaryItems = [new(1, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0],
            Delay = delay
        });
}
