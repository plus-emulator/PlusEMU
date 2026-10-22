using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: Toggle State lists the one-state stairs in its data batch with state 0, next to the
    // block and the colour tile it moves to their next state.
    [Fact]
    public void ToggleStateListsOneStateStairsWithTheirState()
    {
        const uint block = 51, tile = 52, stairs = 53, say = 45, action = 46;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (block, 5, 12, 0.0, 0, "bc_block_0_recolorable", null),
            (tile, 7, 12, 0.0, 0, "wf_colortile", null),
            (stairs, 9, 12, 0.0, 0, "bc_stairs_recolorable", null),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", System.Text.Json.JsonSerializer.Serialize(
                new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [block, tile, stairs], 100))
        };
        var f = new Fixture(layout, live: true);

        // As the furniture table defines them: the block has five states, the stairs one.
        foreach (var (id, modes, height) in new[] { (block, 5, 1.0), (stairs, 1, 0.5) }) {
            var definition = f.Items[id].Definition;
            definition.Height = height;
            definition.Modes = modes;
            definition.InteractionName = "recolorable";
            definition.InteractionType = InteractionType.Recolorable;
            f.Items[id].ExtraData = new RecolorableDataFormat();
        }

        f.Items[tile].ExtraData = new LegacyDataFormat { Data = "0" };

        var rider = f.Walker(1, 6, 10, 6, 11);
        var frames = new List<(uint Header, byte[] Payload)>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        foreach (var state in new[] { "1", "2" }) {
            frames.Clear();
            f.Owned(() => rider.OnChat(0, "tg", false));
            f.Advance(1000);

            var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
            var states = ReadStateBatch(batch.Payload).Where(entry => entry.Id is >= block and <= stairs).ToDictionary(entry => entry.Id, entry => entry.State);
            Assert.Equal(new Dictionary<uint, string> { [block] = state, [tile] = state, [stairs] = "0" }, states);
            Assert.Equal("0", f.Items[stairs].LegacyDataString);
        }
    }

    // Observed on AIR WIN63-202610201032-107964540: Toggle State steps a free block 0, 1, 2, 3, 4, 0, 1. Each step sends its ObjectUpdate
    // with the new height and the HeightMapUpdate for its tile before the data batch, which lists it too. A block with a colour tile on
    // top keeps its state and is not in the batch.
    [Fact]
    public void ToggleStateLowersAFreeBlockAndSkipsABlockWithFurniOnTop()
    {
        const uint free = 51, covered = 52, tileOnTop = 53, say = 45, action = 46;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (free, 5, 12, 0.0, 0, "bc_block_0_recolorable", null),
            (covered, 7, 12, 0.0, 0, "bc_block_0_recolorable", null),
            (tileOnTop, 7, 12, 1.0, 0, "wf_colortile", null),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", System.Text.Json.JsonSerializer.Serialize(
                new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [free, covered], 100))
        };
        var f = new Fixture(layout, live: true);

        // As the furniture table defines it: five states, 1.0 high at state 0 and 0.25 lower per state.
        foreach (var id in new[] { free, covered }) {
            var definition = f.Items[id].Definition;
            definition.Height = 1.0;
            definition.Modes = 5;
            definition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];
            definition.InteractionName = "recolorable";
            definition.InteractionType = InteractionType.Recolorable;
            f.Items[id].ExtraData = new RecolorableDataFormat();
        }

        f.Items[tileOnTop].ExtraData = new LegacyDataFormat { Data = "0" };

        var rider = f.Walker(1, 6, 10, 6, 11);
        var frames = new List<(uint Header, byte[] Payload)>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        foreach (var (state, height, tile) in new[] { ("1", "0.75", 0x00C0), ("2", "0.5", 0x0080), ("3", "0.25", 0x0040), ("4", "0.0", 0x0000),
                     ("0", "1.0", 0x0100), ("1", "0.75", 0x00C0) }) {
            frames.Clear();
            f.Owned(() => rider.OnChat(0, "tg", false));
            f.Advance(1000);

            var sent = frames.Where(frame => frame.Header is ServerPacketHeader.ObjectUpdateComposer or ServerPacketHeader.HeightMapUpdateComposer
                or ServerPacketHeader.ObjectsDataUpdateComposer).ToArray();
            Assert.Equal([ServerPacketHeader.ObjectUpdateComposer, ServerPacketHeader.HeightMapUpdateComposer, ServerPacketHeader.ObjectsDataUpdateComposer],
                sent.Select(frame => frame.Header));
            Assert.Equal((free, "0.0", height), ReadFloorUpdate(sent[0].Payload));
            Assert.Equal(new byte[] { 1, 5, 12, (byte)(tile >> 8), (byte)tile }, sent[1].Payload);
            var states = ReadStateBatch(sent[2].Payload).Where(entry => entry.Id is >= free and <= tileOnTop).ToDictionary(entry => entry.Id, entry => entry.State);
            Assert.Equal(new Dictionary<uint, string> { [free] = state }, states);
            Assert.Equal("0", f.Items[covered].LegacyDataString);
        }
    }

    // A one-state furni keeps its listing when its definition has a height list (as the brick blocks have) and furni stand on it.
    [Fact]
    public void ToggleStateListsACoveredOneStateFurniWithAHeightList()
    {
        const uint block = 51, brick = 52, tileOnTop = 53, say = 45, action = 46;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (block, 5, 12, 0.0, 0, "bc_block_0_recolorable", null),
            (brick, 7, 12, 0.0, 0, "bc_block_redbrick*10", null),
            (tileOnTop, 7, 12, 1.0, 0, "wf_colortile", null),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", System.Text.Json.JsonSerializer.Serialize(
                new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [block, brick], 100))
        };
        var f = new Fixture(layout, live: true);

        var blockDefinition = f.Items[block].Definition;
        blockDefinition.Height = 1.0;
        blockDefinition.Modes = 5;
        blockDefinition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];
        blockDefinition.InteractionName = "recolorable";
        blockDefinition.InteractionType = InteractionType.Recolorable;
        f.Items[block].ExtraData = new RecolorableDataFormat();

        // As the furniture table defines the brick block: one state, with the same height list as the small block.
        var brickDefinition = f.Items[brick].Definition;
        brickDefinition.Height = 1.0;
        brickDefinition.Modes = 1;
        brickDefinition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];
        f.Items[brick].ExtraData = new LegacyDataFormat { Data = "0" };
        f.Items[tileOnTop].ExtraData = new LegacyDataFormat { Data = "0" };

        var rider = f.Walker(1, 6, 10, 6, 11);
        var frames = new List<(uint Header, byte[] Payload)>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        foreach (var state in new[] { "1", "2" }) {
            frames.Clear();
            f.Owned(() => rider.OnChat(0, "tg", false));
            f.Advance(1000);

            var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
            var states = ReadStateBatch(batch.Payload).Where(entry => entry.Id is >= block and <= tileOnTop).ToDictionary(entry => entry.Id, entry => entry.State);
            Assert.Equal(new Dictionary<uint, string> { [block] = state, [brick] = "0" }, states);
            Assert.Equal("0", f.Items[brick].LegacyDataString);
        }
    }

    private static (uint Id, string Z, string Height) ReadFloorUpdate(byte[] payload)
    {
        var id = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var offset = 5 * 4;
        var z = ReadText(payload, ref offset);

        return (id, z, ReadText(payload, ref offset));
    }

    // Legacy entries carry the state as their string, map entries (recolorable furni) under the "state" key.
    private static (uint Id, string State)[] ReadStateBatch(byte[] payload)
    {
        var count = BinaryPrimitives.ReadInt32BigEndian(payload);
        var offset = 4;
        var entries = new List<(uint, string)>();

        for (var i = 0; i < count; i++) {
            var id = BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(offset));
            var format = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset + 4)) & 0xFF;
            offset += 8;

            if (format == (int)FurniDataStructure.Legacy) {
                entries.Add((id, ReadText(payload, ref offset)));
                continue;
            }

            Assert.Equal((int)FurniDataStructure.Map, format);
            var pairs = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(offset));
            offset += 4;
            var state = "";

            for (var pair = 0; pair < pairs; pair++) {
                var key = ReadText(payload, ref offset);
                var value = ReadText(payload, ref offset);
                state = key == "state" ? value : state;
            }

            entries.Add((id, state));
        }

        Assert.Equal(offset, payload.Length);

        return [.. entries];
    }

    private static string ReadText(byte[] payload, ref int offset)
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
        var text = Encoding.UTF8.GetString(payload, offset + 2, length);
        offset += 2 + length;

        return text;
    }
}
