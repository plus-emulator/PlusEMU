using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Write to Logs box only writes a log line: placing it on a running Fast Queue's stack (or anywhere in the room)
    // must not move the queue's tiles off their home tiles.
    [Theory]
    [InlineData(4, 12, 3.06)]  // on the repeater stack
    [InlineData(6, 12, 1.95)]  // on the receiver stack
    [InlineData(9, 15, 0)]     // elsewhere in the room
    public void PlacingALogBoxDoesNotDisturbARunningFastQueue(int x, int y, double z)
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var homes = Enumerable.Range(1, 8).Select(id => (f.Items[(uint)id].GetX, f.Items[(uint)id].GetY)).ToArray();

        f.Advance(1250);
        Assert.Equal(homes, Enumerable.Range(1, 8).Select(id => (f.Items[(uint)id].GetX, f.Items[(uint)id].GetY)));

        f.PlaceWired(40, x, y, z, "wf_act_log");

        for (var pulse = 1; pulse <= 20; pulse++) {
            f.Advance(200);
            Assert.True(homes.SequenceEqual(Enumerable.Range(1, 8).Select(id => (f.Items[(uint)id].GetX, f.Items[(uint)id].GetY))),
                $"pulse {pulse}: " + string.Join(" ", Enumerable.Range(1, 8).Select(id => $"{id}@{f.Items[(uint)id].GetX},{f.Items[(uint)id].GetY}")));
        }
    }
}

public sealed partial class WiredGlideConveyorTests
{
    // Observed r94 F2 (hhus 2026-10-16, AIR WIN63-202610131318-860371703): furni_to_furni moves a mover that already stands on its
    // assigned target back onto the top of that stack; a mover already on top publishes nothing.
    [Fact]
    public void FurniToFurniRestacksAMoverAlreadyOnItsTargetUnlessItIsOnTop()
    {
        var f = new Fixture([
            (1, 5, 5, 0, 0, "bc_tile_1", null),
            (2, 5, 5, 1, 0, "bc_tile_1", null),
            (3, 5, 5, 2, 0, "bc_tile_1", null)
        ], live: true);
        var moved = new List<uint>();

        new Plus.HabboHotel.Items.Wired.Modern.Actions.WiredMovementActions(new Random(3)).Execute("wf_act_furni_to_furni",
            new() { IntParams = [100, 100] }, [f.Items[2], f.Items[3]], [f.Items[1]], [],
            (item, _, _, _, _) => { moved.Add(item.Id); return true; }, (_, _, _, _, _) => false, (_, _) => { });

        Assert.Equal([2u], moved);
    }
}
