using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: Match to Snapshot puts five furni of one stack back on one tile. With the position
    // only, they stack in a new order in nearly every firing (17 orders in 18 firings); with the altitude too, the saved heights are
    // restored and the stack comes back as it was (18 of 18). Restores of the rotation only were not probed and keep one order.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void MatchToSnapshotStacksFurniOnOneTileInARandomOrderUnlessItRestoresTheAltitude(int altitude, bool random)
    {
        var orders = new HashSet<string>();

        for (var firing = 0; firing < 18; firing++) {
            uint[] ids = [40, 41, 42, 43, 44];
            var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>();

            foreach (var id in ids) {
                layout.Add((id, 4 + (int)(id - 40), 10, 0.0, 0, "bc_tile_1", null));
            }

            layout.Add((ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })));
            layout.Add((ObservedActionId, 4, 12, 0.65, 0, "wf_act_match_to_sshot", JsonSerializer.Serialize(new WiredConfiguration
            {
                IntParams = [0, 0, 1, altitude, 100],
                SelectedItems = [.. ids],
                Snapshots = [.. ids.Select(id => new WiredFurniSnapshot(id, 29284, 9, 9, id - 40, 0, ""))]
            })));
            var f = new Fixture(layout.ToArray(), live: true);
            var rider = f.Walker(1, 4, 10, 4, 11);

            f.Owned(() => rider.OnChat(0, "alpha", false));
            f.Advance(500);

            Assert.All(ids, id => Assert.Equal((9, 9), (f.Items[id].GetX, f.Items[id].GetY)));
            Assert.Equal(5, ids.Select(id => f.Items[id].GetZ).Distinct().Count());
            orders.Add(string.Join(' ', ids.OrderBy(id => f.Items[id].GetZ)));
        }

        if (random) {
            Assert.True(orders.Count >= 10, "the stack order changed between firings");
        }
        else {
            Assert.Equal(["40 41 42 43 44"], orders);
        }
    }

    [Fact]
    public void MatchToSnapshotThatRestoresOnlyTheRotationKeepsTheStackOrder()
    {
        var orders = new HashSet<string>();

        for (var firing = 0; firing < 18; firing++) {
            uint[] ids = [40, 41, 42, 43, 44];
            var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>();

            foreach (var id in ids) {
                layout.Add((id, 9, 9, id - 40, 0, "bc_tile_1", null));
            }

            layout.Add((ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })));
            layout.Add((ObservedActionId, 4, 12, 0.65, 0, "wf_act_match_to_sshot", JsonSerializer.Serialize(new WiredConfiguration
            {
                IntParams = [0, 1, 0, 0, 100],
                SelectedItems = [.. ids],
                Snapshots = [.. ids.Select(id => new WiredFurniSnapshot(id, 29284, 9, 9, id - 40, 2, ""))]
            })));
            var f = new Fixture(layout.ToArray(), live: true);
            var rider = f.Walker(1, 4, 10, 4, 11);

            f.Owned(() => rider.OnChat(0, "alpha", false));
            f.Advance(500);

            orders.Add(string.Join(' ', ids.OrderBy(id => f.Items[id].GetZ)));
        }

        Assert.Single(orders);
    }
}
