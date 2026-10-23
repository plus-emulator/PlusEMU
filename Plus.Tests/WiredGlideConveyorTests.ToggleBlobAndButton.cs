using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540 (3 runs, 6 firings each, for wf_blob, wf_blob2, wf_button, wf_button_red and wf_button_x): Toggle
    // State sets a colour tile and a button in its tick batch, sends the blob's new state alone on the next room tick (0.06 to 0.37 s later) and
    // sets the button back to 0 600 ms after it was set, in a packet of its own. A blob starts in state 1 and goes 0, 1, 0 ... on each firing.
    [Fact]
    public void ToggleStateSetsABlobOnTheNextRoomTickAndResetsAButtonAfter600Milliseconds()
    {
        const uint tile = 41, blob = 42, button = 43, say = 45, action = 46;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (tile, 5, 12, 0.0, 0, "wf_colortile", null),
            (blob, 6, 12, 0.0, 0, "wf_blob", null),
            (button, 7, 12, 0.0, 0, "wf_button", null),
            (47, 8, 12, 0.0, 0, "wf_trg_stuff_state", SelectedConfig([0, 100], blob)),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [tile, blob, button], 100))
        };
        var f = new Fixture(layout, live: true);
        f.Items[blob].Definition.Modes = 2;
        f.Items[button].Definition.Modes = 2;
        f.Items[tile].ExtraData = new LegacyDataFormat { Data = "0" };
        f.Items[blob].ExtraData = new LegacyDataFormat { Data = "1" };
        f.Items[button].ExtraData = new LegacyDataFormat { Data = "" };
        var rider = f.Walker(1, 6, 10, 6, 11);
        var frames = new List<(int At, uint Header, byte[] Payload)>();
        var elapsed = 0;
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((elapsed, BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };
        f.Advance(1150);
        frames.Clear();

        var blobStates = new List<string>();

        for (var firing = 0; firing < 3; firing++) {
            var start = elapsed;
            f.Owned(() => rider.OnChat(0, "tg", false));

            for (; elapsed < start + 3000; elapsed += 50) {
                f.Advance(50);
            }

            var batches = frames.Where(frame => frame.At >= start && frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer)
                .Select(frame => (frame.At, Entries: ReadBatch((frame.Header, frame.Payload)))).ToArray();
            var first = batches.First(batch => batch.Entries.Any(entry => entry.Id == tile));
            Assert.Contains(first.Entries, entry => entry.Id == button && entry.Text == "1");
            Assert.DoesNotContain(first.Entries, entry => entry.Id == blob);

            var blobBatch = Assert.Single(batches, batch => batch.Entries.Any(entry => entry.Id == blob));
            // A trigger that watches the blob flashes in the batch of its change.
            Assert.Equal([blob, 47u], blobBatch.Entries.Select(entry => entry.Id).Order());
            blobStates.Add(blobBatch.Entries.Single(entry => entry.Id == blob).Text);
            Assert.InRange(blobBatch.At - first.At, 50, 500);

            var reset = Assert.Single(frames, frame => frame.At >= start && frame.Header == ServerPacketHeader.ObjectDataUpdateComposer);
            Assert.Equal((button, "0"), ReadSingle((reset.Header, reset.Payload)));
            Assert.InRange(reset.At - first.At, 550, 650);
            Assert.Equal("0", f.Items[button].LegacyDataString);
        }

        Assert.Equal(["0", "1", "0"], blobStates);
    }
}
