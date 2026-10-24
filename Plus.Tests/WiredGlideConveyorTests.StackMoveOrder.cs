using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private static string MoveRotateWithTurnConfig(int movement, int rotation, params uint[] items)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, rotation],
            PrimaryItems = [.. items.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = [100],
            Delay = 0
        });
    }

    private static HashSet<string> StackedFiringOrders(int movement, int rotation, int firings)
    {
        var orders = new HashSet<string>();
        uint[] ids = [80, 81, 82, 83];

        for (var firing = 0; firing < firings; firing++) {
            var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>();

            foreach (var id in ids) {
                layout.Add((id, 6, 12, id - 80, 0, "bc_tile_1", null));
            }

            layout.Add((90, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")));
            layout.Add((91, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateWithTurnConfig(movement, rotation, ids)));
            var f = new Fixture(layout.ToArray(), live: true);
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);

            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            Assert.Equal(4, ids.Select(id => f.Items[id].GetZ).Distinct().Count());
            Assert.All(ids, id => Assert.Equal(movement == 9 ? 7 : 6, f.Items[id].GetX));
            orders.Add(string.Join(' ', ids.OrderBy(id => f.Items[id].GetZ)));
        }

        return orders;
    }

    [Fact]
    public void MoveRotateLandsAMovedStackInARandomOrder()
    {
        var orders = StackedFiringOrders(9, 0, 36);

        Assert.True(orders.Count >= 10, $"the stack order changed between firings: {orders.Count} orders");
    }

    [Fact]
    public void MoveRotateTurnsAStackInPlaceInARandomOrder()
    {
        var orders = StackedFiringOrders(0, 1, 36);

        Assert.True(orders.Count >= 10, $"the stack order changed between firings: {orders.Count} orders");
    }

    [Fact]
    public void MoveRotateThatChangesNothingRestacksAStackInARandomOrder()
    {
        var orders = StackedFiringOrders(0, 0, 36);

        Assert.True(orders.Count >= 10, $"the stack order changed between firings: {orders.Count} orders");
    }

    [Fact]
    public void TwoMoveBoxesLandTwoFurniOnOneTileInARandomOrder()
    {
        var orders = new HashSet<string>();

        for (var firing = 0; firing < 36; firing++) {
            var f = new Fixture(
            [
                (80, 6, 12, 0.0, 0, "bc_tile_1", null),
                (81, 8, 12, 0.0, 0, "bc_tile_1", null),
                (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
                (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 80)),
                (85, 4, 14, 1.3, 0, "wf_act_move_rotate", MoveRotateConfig(7, 81))
            ], live: true);
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);

            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            orders.Add(string.Join(' ', new uint[] { 80, 81 }.OrderBy(id => f.Items[id].GetZ)));
        }

        Assert.Equal(2, orders.Count);
    }

    [Fact]
    public void MoveRotateMovesAStackWithAnItemThatCannotBeStackedOnOneFurniAtATime()
    {
        var outcomes = new SortedSet<string>(StringComparer.Ordinal);

        for (var firing = 0; firing < 24; firing++) {
            var f = new Fixture(
            [
                (80, 6, 12, 0.0, 0, "bc_tile_1", null),
                (81, 6, 12, 1.0, 0, "bc_tile_1", null),
                (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
                (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(9, 80, 81))
            ], live: true);
            f.Items[81].Definition.Stackable = false;
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);

            f.Owned(() => rider.OnChat(0, "mv", false));
            f.Advance(300);

            outcomes.Add(string.Join('/', new[] { f.Items[80], f.Items[81] }.Select(item => (item.GetX, item.GetY, item.GetZ))));
        }

        // The upper furni cannot be stacked on, so the lower one stays when the upper one moves first.
        Assert.Equal(["(6, 12, 0)/(7, 13, 0)", "(7, 13, 0)/(7, 13, 1)"], outcomes);
    }
}
