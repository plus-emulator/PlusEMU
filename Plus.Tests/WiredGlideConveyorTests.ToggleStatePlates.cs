using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData("wf_ringplate")]
    [InlineData("wf_arrowplate_8dir")]
    public void ToggleStateLeavesTheRingAndArrowPlatesAlone(string plate)
    {
        const uint tile = 51, target = 52, say = 45, action = 46;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (tile, 5, 12, 0.0, 0, "wf_colortile", null),
            (target, 7, 12, 0.0, 0, plate, null),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", System.Text.Json.JsonSerializer.Serialize(
                new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [tile, target], 100))
        };
        var f = new Fixture(layout, live: true);
        f.Items[target].Definition.Modes = 4;
        f.Items[target].ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        var rider = f.Walker(1, 6, 10, 6, 11);
        var frames = new List<(uint Header, byte[] Payload)>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        for (var firing = 0; firing < 4; firing++) {
            frames.Clear();
            f.Owned(() => rider.OnChat(0, "tg", false));
            f.Advance(1000);

            var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
            var ids = ReadStateBatch(batch.Payload).Select(entry => entry.Id).ToArray();
            Assert.Contains(tile, ids);
            Assert.DoesNotContain(target, ids);
            Assert.Equal("0", f.Items[target].LegacyDataString);
        }
    }
}
