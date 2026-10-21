using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Teleport to Room and Scan Chest Furni by Type as Habbo stores and refuses them. Observed on AIR WIN63-202610201032-107964540.
public sealed partial class WiredNativeLifecycleTests
{
    [Fact]
    public void TeleportToRoomPicksAreOnlyCheckedWhenThePickedFurniAreTheSource()
    {
        var f = new World();
        var colortile = f.Item(2);
        colortile.Definition.InteractionName = "";
        var actionItem = f.Item(1);
        actionItem.Definition.InteractionName = "wf_act_teleport_to_room";
        var action = f.Wired.CreateConfiguredBox(actionItem)!;
        var native = WiredNativeEditorProjection.DefaultNative(action.Descriptor);

        Assert.True(WiredNativeEditorProjection.TryCompile(actionItem.Id, action.Descriptor,
            native with { PrimaryItems = [new(colortile.Id, false)], FurniSourceTypes = [0] }, out var runtime));
        Assert.True(WiredConfigurationSave.TrySave(action, runtime, TestWiredConfigurationStore.Instance, out var error), error);

        Assert.True(WiredNativeEditorProjection.TryCompile(actionItem.Id, action.Descriptor,
            native with { PrimaryItems = [new(colortile.Id, false)], FurniSourceTypes = [100] }, out var picked));
        Assert.False(WiredConfigurationSave.TrySave(action, picked, TestWiredConfigurationStore.Instance, out var refusal));
        Assert.Equal("wiredfurni.error.require_room_linker", refusal);
    }

    [Fact]
    public void TeleportToRoomIgnoresIntsAndTextAndKeepsTheUserSourceRules()
    {
        var saved = Save("wf_act_teleport_to_room", [5], text: "x");
        Assert.True(saved.Accepted, Show(saved));
        Assert.Empty(saved.Ints);
        Assert.Equal("", saved.Text);

        foreach (var source in new[] { 0, 10, 11, 200, 201 }) {
            Assert.Equal([source], Save("wf_act_teleport_to_room", [], users: [source]).Users);
        }

        Assert.Equal("wiredfurni.error.invalid_user_source", Save("wf_act_teleport_to_room", [], users: [100]).Key);
        Assert.Equal("wiredfurni.error.invalid_furni_source", Save("wf_act_teleport_to_room", [], furni: [101]).Key);
        Assert.Equal("wiredfurni.error.invalid_furni_source", Save("wf_act_teleport_to_room", [], furni: [901]).Key);
        Assert.Equal("wiredfurni.error.invalid_furni_source_count", Save("wf_act_teleport_to_room", [], furni: [100, 100]).Key);

        foreach (var source in new[] { 0, 100, 200, 201 }) {
            Assert.Equal([source], Save("wf_act_teleport_to_room", [], furni: [source]).Furni);
        }
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(5, 1)]
    [InlineData(-1, 1)]
    public void ScanChestStoresTheScanningModeZeroOrOne(int sent, int stored) => Assert.Equal([stored], Save("wf_xtra_scan_chest_furni_by_type", [sent]).Ints);

    [Fact]
    public void ScanChestFillsAMissingModeAndIgnoresText()
    {
        Assert.Equal([0], Save("wf_xtra_scan_chest_furni_by_type", []).Ints);
        var saved = Save("wf_xtra_scan_chest_furni_by_type", [1], text: "abc");
        Assert.True(saved.Accepted);
        Assert.Equal("", saved.Text);
    }

    [Theory]
    [InlineData(101, 101)]
    [InlineData(100, 101)]
    [InlineData(200, 101)]
    public void ScanChestRefusesTheSecondPickListAsTheChestSource(int types, int chests)
    {
        var refused = Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [types, chests]);
        Assert.False(refused.Accepted);
        Assert.False(refused.Dropped);
        Assert.Equal("wiredfurni.error.require_furni_chests", refused.Key);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 100)]
    public void ScanChestDropsASaveWhoseItemTypeSourceIsOneWithoutAnyReply(int types, int chests) =>
        Assert.True(Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [types, chests]).Dropped);

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 100)]
    [InlineData(200, 200)]
    [InlineData(201, 201)]
    [InlineData(0, 0)]
    [InlineData(101, 100)]
    public void ScanChestAcceptsTheSourcePairsHabboAccepts(int types, int chests) =>
        Assert.Equal([types, chests], Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [types, chests]).Furni);

    [Fact]
    public void ScanChestKeepsItsOtherSourceKeys()
    {
        Assert.Equal([100, 100], Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [100]).Furni);
        Assert.Equal("wiredfurni.error.invalid_furni_source", Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [901, 901]).Key);
        Assert.Equal("wiredfurni.error.invalid_furni_source_count", Save("wf_xtra_scan_chest_furni_by_type", [0], furni: [0, 100, 100]).Key);
        Assert.Equal("wiredfurni.error.invalid_user_source_count", Save("wf_xtra_scan_chest_furni_by_type", [0], users: [0]).Key);
    }
}
