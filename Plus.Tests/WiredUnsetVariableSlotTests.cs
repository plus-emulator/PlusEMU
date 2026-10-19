using System.Linq;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: the hotel opens a fresh Furni/Users With Variable editor with variable ids ["0", "0"].
public class WiredUnsetVariableSlotTests
{
    [Fact]
    public void EveryFreshVariableSlotIsTheHotelsZero()
    {
        var boxes = WiredBoxRegistry.All
            .Where(descriptor => WiredNativeEditorProjection.Supports(descriptor.CanonicalName))
            .Select(descriptor => (descriptor.CanonicalName, Ids: WiredNativeEditorProjection.DefaultNative(descriptor).VariableIds))
            .Where(box => box.Ids.Length > 0)
            .ToList();

        Assert.Contains(boxes, box => box.CanonicalName == "wf_slc_furni_with_var");
        Assert.All(boxes, box => Assert.All(box.Ids, id => Assert.Equal("0", id)));
    }

    [Theory]
    [InlineData("wf_slc_furni_with_var")]
    [InlineData("wf_slc_users_with_var")]
    public void WithVariableFrameSendsZeroForBothSlots(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        Assert.Equal(["0", "0"], WiredNativeEditorProjection.DefaultNative(descriptor).VariableIds.ToArray());
    }

    [Theory]
    [InlineData("wf_slc_furni_with_var")]
    [InlineData("wf_slc_users_with_var")]
    [InlineData("wf_act_give_var")]
    [InlineData("wf_cnd_var_val_match")]
    public void SavedNSlotsStillCompileLikeZero(string name)
    {
        var zero = WiredOctoberEditorParityTests.Compile(name, native => native with { VariableIds = [.. native.VariableIds.Select(_ => "0")] });
        var saved = WiredOctoberEditorParityTests.Compile(name, native => native with { VariableIds = [.. native.VariableIds.Select(_ => "n")] });

        Assert.Equal(zero.IntParams.ToArray(), saved.IntParams.ToArray());
        Assert.All(zero.VariableIds, id => Assert.True(WiredVariableAbsent.Is(id)));
        Assert.All(saved.VariableIds, id => Assert.True(WiredVariableAbsent.Is(id)));
    }
}
