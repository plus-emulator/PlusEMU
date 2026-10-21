using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): editor frame defaults, wall flags and the pick limit of a fresh box.
public class WiredEditorFrameDefaultsTests
{
    private static int[] Defaults(string name) => WiredNativeEditorProjection.FrameDefaults(name, WiredNativeEditorProjection.Metadata(name)).ToArray();

    [Theory]
    [InlineData("wf_act_match_to_sshot", new[] { 0, 0, 0, 0, 1 })]
    [InlineData("wf_act_move_rotate", new[] { 0, 0, 1 })]
    [InlineData("wf_cnd_time_less_than", new[] { 20 })]
    [InlineData("wf_cnd_time_more_than", new[] { 20 })]
    [InlineData("wf_slc_furni_with_var", new[] { 2, 0, 0, 0, -10, 0 })]
    [InlineData("wf_slc_users_with_var", new[] { 2, 0, 0, 0, -10, 0 })]
    [InlineData("wf_slc_furni_neighborhood", new[] { 1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    [InlineData("wf_slc_users_neighborhood", new[] { 1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    public void FrameDefaultsMatchTheHotel(string name, int[] expected) => Assert.Equal(expected, Defaults(name));

    [Theory]
    [InlineData("wf_slc_furni_neighborhood")]
    [InlineData("wf_slc_users_neighborhood")]
    public void NeighborhoodReadsTheSameSeventeenFieldsWithOrWithoutTheEmptyWord(string name)
    {
        int[] saved = [1, 0, 0, 33554431, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        var fresh = WiredOctoberEditorParityTests.Compile(name, native => native);
        var old = WiredOctoberEditorParityTests.Compile(name, native => native with { OwnedIntParams = [.. saved] });

        Assert.Equal(saved, fresh.IntParams.ToArray());
        Assert.Equal(saved, old.IntParams.ToArray());

        // Observed on AIR WIN63-202610201032-107964540: Furni Neighborhood accepts an 18th int in a save and stores it as 0.
        if (name == "wf_slc_furni_neighborhood") {
            var extra = WiredOctoberEditorParityTests.Compile(name, native => native with { OwnedIntParams = [.. saved, 1] });
            Assert.Equal(saved, extra.IntParams.ToArray());
        }
        else {
            Assert.False(WiredOctoberEditorParityTests.TryCompile(name, native => native with { OwnedIntParams = [.. saved, 1] }));
        }
    }

    [Theory]
    [InlineData("wf_slc_furni_with_var")]
    [InlineData("wf_slc_users_with_var")]
    public void WithVariableSixthZeroKeepsTheFiveIntRuntime(string name) => Assert.Equal(
        WiredOctoberEditorParityTests.Compile(name, native => native with { OwnedIntParams = [2, 0, 0, 0, -10] }).IntParams.ToArray(),
        WiredOctoberEditorParityTests.Compile(name, native => native).IntParams.ToArray());

    [Fact]
    public void NotFurniOnSendsNoInvertFlag()
    {
        Assert.False(WiredNativeEditorProjection.Metadata("wf_cnd_not_furni_on").Invert);
        Assert.True(WiredNativeEditorProjection.Metadata("wf_cnd_not_hv_avtrs").Invert);
    }

    [Theory]
    [InlineData("wf_act_toggle_state", true)]
    [InlineData("wf_act_furni_to_furni", true)]
    [InlineData("wf_cnd_match_snapshot", true)]
    [InlineData("wf_cnd_not_match_snap", true)]
    [InlineData("wf_act_move_rotate", true)]
    [InlineData("wf_slc_furni_bytype", true)]
    [InlineData("wf_slc_furni_picks", true)]
    [InlineData("wf_trg_click_furni", true)]
    [InlineData("wf_trg_state_changed", true)]
    [InlineData("wf_trg_stuff_state", true)]
    [InlineData("wf_xtra_text_output_furni_name", true)]
    [InlineData("wf_act_cancel_transaction", false)]
    [InlineData("wf_cnd_slc_quantity", false)]
    public void WallFlagsMatchTheHotel(string name, bool wall) => Assert.Equal(wall, WiredNativeEditorProjection.Metadata(name).AllowWall);

    [Fact]
    public void StoredRowKeepsWallPicksAfterTheCardStopsAllowingThem()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_cancel_transaction", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { PrimaryItems = [new(5, true)] };

        Assert.False(WiredNativeEditorProjection.TryCompile(1, descriptor, native, out _));
        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, native, out _, storedRow: true));
    }

    [Fact]
    public void EditorAdvertisesTwentyPicksAndTheServerKeepsAHundred()
    {
        Assert.Equal(20, WiredConfigurationLimits.EditorFurniLimit);
        Assert.Equal(100, WiredConfigurationLimits.SelectedItems);
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): a wall torch at :w=5,0 l=8,40 r saved with state matching only.
    [Theory]
    [InlineData("wf_cnd_match_snapshot")]
    [InlineData("wf_cnd_not_match_snap")]
    public void SnapshotConditionFrameCarriesTheWallSnapshotText(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var wall = new WiredFurniSnapshot(2147418112, 9, 0, 0, 0, 0, "0")
        {
            Wall = new WiredWallSnapshot(5, 0, 8, 40, false, null) { CapturedAltitudeHundredths = 247 }
        };
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [1, 0, 0, 0],
            PrimaryItems = [new(2147418112, true)],
            SavedState = new() { Snapshots = [wall] }
        };

        Assert.Equal("-2147418112,0,5,0,8,247,r", WiredNativeEditorProjection.HotelFrame(descriptor, native).Text);
        // Floor entries, observed the same way: altitude in hundredths, highest id first.
        Assert.Equal("2147418155,0,4,9,11,100;2147418149,3,0,8,11,0;2147418148,0,2,9,11,0", WiredNativeEditorProjection.SnapshotText([
            new WiredFurniSnapshot(2147418148, 9, 9, 11, 0, 2, "0"), new WiredFurniSnapshot(2147418155, 9, 9, 11, 1.0, 4, ""),
            new WiredFurniSnapshot(2147418149, 8, 8, 11, 0, 0, "3")]));
        Assert.Equal("", WiredNativeEditorProjection.HotelFrame(descriptor, WiredNativeEditorProjection.DefaultNative(descriptor)).Text);
    }
}
