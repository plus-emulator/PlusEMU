using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredNativeLifecycleTests
{
    // Observed r92g r92-signals (AIR WIN63-202610131318-860371703): send_signal saved with one furni source [100] reopened as
    // [100, 200]; r92i r92-signals saved both slots and reopened them unchanged. r92f r92-valid-moves: a furni source on a box
    // without furni slots is refused with wiredfurni.error.invalid_furni_source_count (frame 20502).
    [Fact]
    public void MissingFurniSourceSlotsTakeTheirDefaultsAndExtraSlotsAreRefused()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_send_signal", out var signal));
        var oneSlot = WiredNativeEditorProjection.DefaultNative(signal) with { FurniSourceTypes = [100] };
        Assert.True(WiredNativeEditorProjection.TryFillSourceSlots(signal, oneSlot, out var filled, out var error));
        Assert.Null(error);
        Assert.Equal([100, 200], filled.FurniSourceTypes.ToArray());
        var both = WiredNativeEditorProjection.DefaultNative(signal) with { FurniSourceTypes = [101, 201] };
        Assert.True(WiredNativeEditorProjection.TryFillSourceSlots(signal, both, out filled, out _));
        Assert.Equal([101, 201], filled.FurniSourceTypes.ToArray());

        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_valid_moves", out var validMoves));
        var extra = WiredNativeEditorProjection.DefaultNative(validMoves) with { FurniSourceTypes = [100] };
        Assert.False(WiredNativeEditorProjection.TryFillSourceSlots(validMoves, extra, out _, out error));
        Assert.Equal("wiredfurni.error.invalid_furni_source_count", error);
    }
}
