using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // The editor has no furni source: a picked source is refused with wiredfurni.error.invalid_furni_source_count. Saved without
    // one, the condition passes on a say trigger.
    [Fact]
    public void ValidMovesWithoutAFurniSourcePassesOnASayTrigger()
    {
        var (room, _, _) = World();
        var box = Box(room, "wf_cnd_valid_moves", _ => null);
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor);
        Assert.Empty(native.FurniSourceTypes);
        Assert.True(WiredNativeEditorProjection.TryCompile(box.Item.Id, box.Descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var config, out var error), error);
        box.ApplyConfiguration(config);

        Assert.True(box.Execute(Context(room, [], [])));
    }
}
