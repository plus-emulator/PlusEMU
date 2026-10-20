using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610151745-318700621: a say signals A1, R1 toggles T1 and signals A2, R2 toggles T2 and signals A3, and
    // R3 toggles T3 and signals A1 again. Each receiving stack ran 100 times (each 7-state tile went from 0 to 2), and the whole
    // cascade flashed in one ObjectsDataUpdate. A 1-stack loop did the same. The tiles here have 1000 states, so a state is a run count.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ASignalLoopEnteredFromASayRunsEachStackAHundredTimesInOneBatch(int stacks)
    {
        const uint say = 70, sayAction = 71;
        var antennas = Enumerable.Range(60, stacks).Select(id => (uint)id).ToArray();
        var tiles = Enumerable.Range(63, stacks).Select(id => (uint)id).ToArray();
        var receivingStacks = Enumerable.Range(80, stacks * 3).Select(id => (uint)id).ToArray();
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "ch" })),
            (sayAction, 4, 9, 0.65, 0, "wf_act_send_signal", SignalSender(antennas[0]))
        };

        for (var i = 0; i < stacks; i++) {
            layout.Add((antennas[i], 9 + i, 13, 0.0, 0, "wf_antenna2", null));
            layout.Add((tiles[i], 5 + i, 12, 0.0, 0, "wf_colortile", null));
            layout.Add((receivingStacks[i * 3], 5 + i, 9, 0.0, 0, "wf_trg_recv_signal", SignalReceiver(antennas[i])));
            layout.Add((receivingStacks[i * 3 + 1], 5 + i, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [tiles[i]], 100)));
            layout.Add((receivingStacks[i * 3 + 2], 5 + i, 9, 1.3, 0, "wf_act_send_signal", SignalSender(antennas[(i + 1) % stacks])));
        }

        var f = new Fixture(layout.ToArray(), live: true, colourTileModes: 1000);

        foreach (var tile in tiles) {
            f.Items[tile].ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        }

        var rider = f.Walker(1, 6, 12, 6, 13);
        var batches = new List<uint[]>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.Add([.. ReadBatch(frame).Select(entry => entry.Id)]);
            }

            return false;
        };

        f.Owned(() => rider.OnChat(0, "ch", false));
        f.Advance(2000);

        Assert.All(tiles, tile => Assert.Equal("100", f.Items[tile].LegacyDataString));
        var cascade = Assert.Single(batches, batch => batch.Intersect([.. antennas, .. tiles, .. receivingStacks]).Any());
        Assert.Superset(new HashSet<uint>([.. antennas, .. tiles, say, sayAction, .. receivingStacks]), cascade.ToHashSet());
    }

    private static string SignalReceiver(uint antenna)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_recv_signal", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            PrimaryItems = [new WiredNativeItemReference(antenna, false)],
            FurniSourceTypes = [100]
        });
    }

    private static string SignalSender(uint antenna)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_send_signal", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0, 0],
            PrimaryItems = [new WiredNativeItemReference(antenna, false)],
            FurniSourceTypes = [100, 200],
            UserSourceTypes = [200],
            Delay = 0
        });
    }
}
