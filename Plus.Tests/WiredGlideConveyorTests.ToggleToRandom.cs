using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540 (3 of 3 runs, 24 firings each): Toggle to Random sets a random state of the state
    // furni in its list (colour tile 0 to 6, floor switch 1 0 to 1, floor switch 2 0 to 3, number tile 0 to 9, glow ball 0 to 1, block
    // 0 to 4, knob 0 to 7, glass door 0 to 1, slider 0 to 3, maze 0 to 3). It never changes the game tiles, the pyramid, the blobs, the
    // box, the buttons, the arrow plates, the pressure plates, the ring plate, the antennas, a game counter or a Wired box that is not part
    // of the running stack.
    [Fact]
    public void ToggleToRandomChangesStateFurniAndLeavesTheOthersAlone()
    {
        const uint say = 45, action = 46, other = 44;
        (uint Id, string Name, int Modes)[] changed = [(41, "wf_colortile", 7), (42, "wf_floor_switch1", 2), (43, "wf_floor_switch2", 4), (47, "wf_numbertile1", 10),
            (48, "wf_glowball", 2), (49, "bc_block_0_recolorable", 5), (50, "wf_knob", 8),
            (59, "wf_glassdoor", 2), (60, "wf_slider", 4), (61, "wf_maze", 4)];
        (uint Id, string Name, int Modes)[] untouched = [(51, "wf_tile1", 10), (52, "wf_tile2", 10), (53, "wf_pyramid", 4), (54, "wf_blob", 4), (55, "wf_box", 2),
            (56, "wf_button", 2), (57, "wf_arrowplate", 4), (58, "wf_pressureplate", 4), (62, "wf_blob2", 4), (63, "wf_button_red", 2),
            (64, "wf_arrowplate_8dir", 4), (65, "wf_ringplate", 4), (66, "wf_antenna1", 4), (67, "wf_antenna2", 4), (68, "wf_game_upcounter1", 2)];
        var all = changed.Concat(untouched).ToArray();
        var layout = all.Select((tile, index) => (tile.Id, 4 + index % 8, 10 + index / 8, 0.0, 0, tile.Name, (string?)null))
            .Append((other, 9, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "never" })))
            .Append((say, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })))
            .Append((action, 4, 9, 0.65, 0, "wf_act_toggle_to_rnd", ObservedNative("wf_act_toggle_to_rnd", [], [.. all.Select(tile => tile.Id), other], 100)))
            .ToArray();
        var f = new Fixture(layout, live: true);

        foreach (var tile in all) {
            f.Items[tile.Id].Definition.Modes = tile.Modes;
            f.Items[tile.Id].ExtraData = new LegacyDataFormat { Data = "0" };
        }

        var otherBefore = f.Items[other].LegacyDataString;
        var rider = f.Walker(1, 6, 10, 6, 11);
        var batches = new List<(uint Id, string Text)[]>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();

            if (BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.Add(ReadBatch((ServerPacketHeader.ObjectsDataUpdateComposer, bytes[6..])));
            }

            return false;
        };

        for (var firing = 0; firing < 40; firing++) {
            f.Owned(() => rider.OnChat(0, "tg", false));
            f.Advance(3000);
        }

        var sent = batches.SelectMany(batch => batch).ToArray();

        foreach (var tile in changed) {
            var states = sent.Where(entry => entry.Id == tile.Id).Select(entry => int.Parse(entry.Text)).ToArray();
            Assert.NotEmpty(states);
            Assert.All(states, state => Assert.InRange(state, 0, tile.Modes - 1));
            Assert.True(states.Distinct().Count() > 1, tile.Name);
        }

        foreach (var tile in untouched) {
            Assert.DoesNotContain(sent, entry => entry.Id == tile.Id);
            Assert.Equal("0", f.Items[tile.Id].LegacyDataString);
        }

        // The box of another stack is not part of the toggle; only the firing stack flashes.
        Assert.DoesNotContain(sent, entry => entry.Id == other);
        Assert.Equal(otherBefore, f.Items[other].LegacyDataString);
    }
}
