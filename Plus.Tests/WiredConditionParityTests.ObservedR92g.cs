using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredConditionParityTests
{
    // Observed r92g r92-valid-moves (AIR WIN63-202610131318-860371703): the hotel editor has no furni source (a picked source is
    // refused with wiredfurni.error.invalid_furni_source_count, r92f frame 20502); saved without one, the condition passed on a
    // say trigger in a-w1, b-w1 and c-w1.
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
