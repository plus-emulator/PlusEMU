using System.Text.Json;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Official stored editor contracts: 3db3d3c17 (click cards), f8f1f95f4 (move/rotate user).
// These checks cover source admission and editor publication, not official execution semantics.
public sealed class WiredObservedEditorSourceTests
{
    [Theory]
    [InlineData("wf_trg_click_tile", 100)]
    [InlineData("wf_trg_click_tile", 200)]
    [InlineData("wf_act_click_conf", 0)]
    [InlineData("wf_act_click_conf", 200)]
    [InlineData("wf_act_click_conf", 201)]
    [InlineData("wf_act_move_rotate_user", 0)]
    [InlineData("wf_act_move_rotate_user", 200)]
    [InlineData("wf_act_move_rotate_user", 201)]
    public void ObservedSourcesSurviveNativeSaveGrammarAndStoredEditorRoundTrip(string name, int source)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var fields = name == "wf_trg_click_tile"
            ? new object[] { 0, "", 0, 1, source, 0, 0, 0 }
            : new object[] { 2, name == "wf_act_click_conf" ? 0 : -1, name == "wf_act_click_conf" ? 0 : -1,
                "", 0, 0, 0, 1, source, 0, 0 };
        var packet = HabbiconTestSupport.Incoming(fields);
        Assert.True(WiredLegacyProtocol.TryReadNative(packet, descriptor.Category, out var parsed));
        var native = parsed with { NativeCode = descriptor.EditorCode };
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out var runtime));
        Assert.Equal(new[] { source }, name == "wf_trg_click_tile" ? runtime.IntParams : runtime.UserSources.Values);

        var stored = JsonSerializer.Deserialize<WiredNativeEditorConfiguration>(JsonSerializer.Serialize(runtime.Origin!.Native))!;
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, stored, out var restored));
        Assert.True(WiredNativeEditorProjection.Matches(runtime, restored));
        var output = new HabbiconTestSupport.RecordingPacket();
        var snapshot = new WiredEditorSnapshot(7, 91, descriptor, restored, 20, []) { Native = stored };

        if (descriptor.Category == WiredBoxCategory.Trigger) {
            new WiredTriggeRconfigComposer(snapshot).Compose(output);
            Assert.Equal(new object[] { true, 1, 2, 100, 200, 0, 1, 100, 0, false, 0, 0 }, output.Writes.TakeLast(12));
        }
        else {
            new WiredEffectConfigComposer(snapshot).Compose(output);
            Assert.Equal(new object[] { true, 0, 1, 3, 0, 200, 201, 0, 1, 0, false, 0, 2,
                name == "wf_act_click_conf" ? 0 : -1, name == "wf_act_click_conf" ? 0 : -1 }, output.Writes.TakeLast(15));
        }
    }

    [Theory]
    [InlineData("wf_trg_click_tile", 0)]
    [InlineData("wf_trg_click_tile", 201)]
    [InlineData("wf_act_click_conf", 10)]
    [InlineData("wf_act_click_conf", 11)]
    [InlineData("wf_act_move_rotate_user", 10)]
    [InlineData("wf_act_move_rotate_user", 11)]
    public void SourcesAbsentFromObservedEditorAreRejected(string name, int source)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = descriptor.Category == WiredBoxCategory.Trigger
            ? native with { FurniSourceTypes = [source] }
            : native with { UserSourceTypes = [source] };
        Assert.False(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(201)]
    public void ClickFurnitureRetainsItsSeparateSourceContract(int source)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_click_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { FurniSourceTypes = [source] };
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
    }
}
