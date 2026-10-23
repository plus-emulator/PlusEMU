using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Xunit;

namespace Plus.Tests;

public sealed class WiredSelectorWallTests
{
    // A wall exemplar selects wall furniture of that type.
    [Theory]
    [InlineData(false, new uint[] { 1, 2 })]
    [InlineData(true, new uint[] { 1 })]
    public void ByTypeSelectsWallFurnitureAndRespectsState(bool matchState, uint[] expected)
    {
        var world = new WiredSelectorWorld(2, 2,
        [new(1, 20, "torch", "0", 0, 0, 0, 1, [], IsFloor: false),
         new(2, 20, "torch", "1", 0, 0, 0, 1, [], IsFloor: false),
         new(3, 21, "floor", "0", 0, 0, 0, 1, [(0, 0)])], []);
        var result = WiredSelectorModule.SelectRaw("wf_slc_furni_bytype",
            new() { SelectedItems = [1], IntParams = [0, matchState ? 1 : 0] }, world, new(new(), new(), new()));
        Assert.Equal(expected, result.Selection.FurniIds.Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvertedPicksKeepTheDistinctWallIdentityWhenFloorWireIdMatches(bool filter)
    {
        var floorId = TemporaryItemIds.First;
        var wallId = TemporaryItemIds.FromWire(floorId, true);
        var world = new WiredSelectorWorld(2, 2,
        [new(floorId, 1, "floor", "0", 0, 0, 0, 1, [(0, 0)]),
         new(wallId, 2, "wall", "0", 0, 0, 0, 1, [], IsFloor: false)], []);
        var pool = new WiredSelectedIds();

        if (filter) {
            pool.FurniIds.UnionWith([floorId, wallId]);
        }

        var input = new WiredSelectorInputs(new(), pool, new(), FurniModified: true);
        var configuration = new WiredConfiguration { SelectedItems = [floorId], IntParams = [filter ? 1 : 0, 1] };
        var raw = WiredSelectorModule.SelectRaw("wf_slc_furni_picks", configuration, world, input);
        var result = WiredSelectorModule.Compose(raw, world, input);
        Assert.Equal(new[] { wallId }, result.FurniIds);
    }
}
