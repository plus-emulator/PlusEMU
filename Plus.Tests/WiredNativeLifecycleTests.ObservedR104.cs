using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredNativeLifecycleTests
{
    // User Clicks Tile checks that its picks are click tiles only when the picks are its source; with the selector as source a
    // plain furni pick saves.
    [Fact]
    public void ClickTileTriggerChecksItsPicksOnlyWhenThePicksAreTheSource()
    {
        var f = new World();
        var plain = f.Item(2);
        plain.Definition.InteractionName = "";
        var triggerItem = f.Item(1);
        triggerItem.Definition.InteractionName = "wf_trg_click_tile";
        var trigger = f.Wired.CreateConfiguredBox(triggerItem)!;
        var native = WiredNativeEditorProjection.DefaultNative(trigger.Descriptor);

        Assert.True(WiredNativeEditorProjection.TryCompile(triggerItem.Id, trigger.Descriptor,
            native with { PrimaryItems = [new(plain.Id, false)], FurniSourceTypes = [200] }, out var runtime));
        Assert.True(WiredConfigurationSave.TrySave(trigger, runtime, TestWiredConfigurationStore.Instance, out var error), error);
        Assert.True(WiredNativeEditorProjection.TryCompile(triggerItem.Id, trigger.Descriptor,
            native with { PrimaryItems = [new(plain.Id, false)], FurniSourceTypes = [100] }, out runtime));
        Assert.False(WiredConfigurationSave.TrySave(trigger, runtime, TestWiredConfigurationStore.Instance, out error));
        Assert.Equal("wiredfurni.error.require_click_tiles", error);
    }
}
