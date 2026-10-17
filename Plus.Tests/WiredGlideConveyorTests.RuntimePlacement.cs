using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
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
        var moved = new List<(uint Id, double? Height)>();

        new Plus.HabboHotel.Items.Wired.Modern.Actions.WiredMovementActions(new Random(3)).Execute("wf_act_furni_to_furni",
            new() { IntParams = [100, 100] }, [f.Items[2], f.Items[3]], [f.Items[1]], [],
            (item, _, _, _, height) => { moved.Add((item.Id, height)); return true; }, (_, _, _, _, _) => false, (_, _) => { });

        // The room ignores a same-tile move without a new height, so the restack names the top of the stack.
        Assert.Equal([(2u, (double?)f.Items[3].TotalHeight)], moved);
    }
}

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610131318-860371703: on a 200 ms Fast Queue a user walking against the queue walks tile by tile to the
    // start with no carry; carrying resumes once the walk has ended.
    [Fact]
    public void FastQueueDoesNotCarryAUserWhoIsWalkingAgainstIt()
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var rider = f.Walker(1, 10, 10, 10, 11);
        f.Owned(() => rider.MoveTo(4, 11));
        f.DrainMovement();
        var reached = false;

        for (var elapsed = 0; elapsed < 6000 && !reached; elapsed += 50) {
            f.Advance(50);
            reached = (rider.X, rider.Y) == (4, 11);
        }

        Assert.True(reached, $"rider stopped at ({rider.X},{rider.Y})");
    }
}

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610131318-860371703: with a 200 ms Fast Queue running, placing a Write to Logs box on either stack,
    // moving it between stacks and saving it never disturbs the queue. Every pulse publishes one WiredMovements entry per tile,
    // home to home.
    [Theory]
    [InlineData("none")]
    [InlineData("place")]
    [InlineData("save")]
    [InlineData("move")]
    [InlineData("elsewhere")]
    public void ChangingAnotherBoxKeepsTheQueuePublishingHomeToHome(string change)
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var observer = f.Walker(2, 6, 9, 6, 10);
        f.Advance(1250);
        var packets = CapturePublication(observer);
        var log = change == "none" ? null! : change == "elsewhere" ? f.PlaceWired(40, 9, 15, 0, "wf_act_log") : f.PlaceWired(40, 4, 12, 3.06, "wf_act_log");

        if (change == "save") {
            var box = (Plus.HabboHotel.Items.Wired.Configuration.IWiredConfiguredItem)f.Box(40);
            f.Owned(() => Assert.True(f.Room.GetWired().PublishConfigured(box, box.Configuration, () => { })));
        }
        else if (change == "move") {
            f.Owned(() => Assert.True(f.Room.GetRoomItemHandler().SetFloorItem(log, 6, 12, 3.06)));
        }

        for (var pulse = 1; pulse <= 20; pulse++) {
            packets.Clear();
            f.Advance(200);
            var furniture = ReadPublication(packets).Where(move => move.Type == 1).ToArray();
            Assert.All(furniture, move => Assert.True(move.FromX == move.ToX && move.ToY == 11,
                $"pulse {pulse}: #{move.Id} {move.FromX}->{move.ToX},{move.ToY} z {move.FromZ}->{move.ToZ}"));
            Assert.Equal(Enumerable.Range(4, 8), Enumerable.Range(1, 8).Select(id => f.Items[(uint)id].GetX));
        }
    }
}

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610151745-318700621 (wf_act_furni_to_furni.spread): four movers sent to two targets by one say arrive in one
    // WiredMovements packet, including the movers restacked on the target they already stood on.
    [Fact]
    public void FurniToFurniPublishesAFiringsMoversInOnePacket()
    {
        var f = new Fixture([
            (1, 8, 9, 0, 0, "bc_tile_1", null),
            (2, 8, 11, 0, 0, "bc_tile_1", null),
            (3, 6, 9, 0, 0, "bc_tile_1", null),
            (4, 6, 10, 0, 0, "bc_tile_1", null),
            (5, 6, 11, 0, 0, "bc_tile_1", null),
            (6, 6, 12, 0, 0, "bc_tile_1", null),
            (10, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (11, 4, 12, 0.65, 0, "wf_act_furni_to_furni", JsonSerializer.Serialize(new WiredConfiguration
            {
                IntParams = [100, 100], Text = "1;2", SelectedItems = [3, 4, 5, 6]
            }))
        ], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = new List<int>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();

            if (BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer) {
                packets.Add(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(6)));
            }

            return false;
        };

        for (var firing = 0; firing < 3; firing++) {
            f.Owned(() => rider.OnChat(0, "alpha", false));
            f.Advance(500);
        }

        Assert.NotEmpty(packets);
        Assert.All(packets, count => Assert.True(count >= 2, $"movements per packet: {string.Join(",", packets)}"));
    }
}
