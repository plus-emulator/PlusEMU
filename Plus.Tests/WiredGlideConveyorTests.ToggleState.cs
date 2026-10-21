using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: Toggle State changes colour tiles and other state furni, but never a floor
    // switch, an antenna or a few game tiles. It also toggles every Wired box in its list, whatever the box type, and the box
    // shows the change as its normal flash (a box that flashes in the same tick still shows one change).
    [Fact]
    public void ToggleStateSkipsSwitchesAndAntennasAndTogglesEveryWiredBox()
    {
        const uint tile = 41, floorSwitch = 42, antenna = 43, otherTrigger = 44, say = 45, action = 46, arrowPlate = 47, selector = 48;
        var skipped = new[] { floorSwitch, antenna, arrowPlate };
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (tile, 5, 12, 0.0, 0, "wf_colortile", null),
            (floorSwitch, 6, 12, 0.0, 0, "wf_floor_switch1", null),
            (antenna, 7, 12, 0.0, 0, "wf_antenna2", null),
            (arrowPlate, 8, 12, 0.0, 0, "wf_arrowplate", null),
            (otherTrigger, 9, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "never" })),
            (selector, 9, 10, 0.0, 0, "wf_slc_furni_area", null),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [tile, floorSwitch, antenna, arrowPlate, otherTrigger, selector], 100))
        };

        var f = new Fixture(layout.ToArray(), live: true);

        // The real definitions of these furni have more than one state: that is why the old rule toggled them.
        foreach (var id in skipped) {
            f.Items[id].Definition.Modes = 2;
        }

        foreach (var id in new[] { tile, floorSwitch, antenna, arrowPlate }) {
            f.Items[id].ExtraData = new LegacyDataFormat { Data = "0" };
        }

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

        f.Owned(() => rider.OnChat(0, "tg", false));
        f.Advance(1000);

        Assert.Equal("1", f.Items[tile].LegacyDataString);
        Assert.All(skipped, id => Assert.Equal("0", f.Items[id].LegacyDataString));
        var sent = batches.SelectMany(batch => batch).ToArray();
        Assert.All(skipped, id => Assert.DoesNotContain(sent, entry => entry.Id == id));
        Assert.Contains(sent, entry => entry.Id == tile && entry.Text == "1");
        // One change per box: the toggle and the flash of the other trigger are the same flip.
        Assert.Single(sent, entry => entry.Id == otherTrigger);
        Assert.Equal("1", f.Items[otherTrigger].LegacyDataString);
        Assert.Single(sent, entry => entry.Id == selector);
        Assert.Equal("1", f.Items[selector].LegacyDataString);
        // The firing boxes flash once, not twice, although the action lists neither of them.
        Assert.Equal("1", f.Items[action].LegacyDataString);
        Assert.Equal("1", f.Items[say].LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540: the boxes of the stack that is running flash, and Toggle State then flips them again,
    // so a box in its own list ends in the toggled state, not in the flash state.
    [Fact]
    public void ToggleStateFlipsTheBoxesOfItsOwnStackAfterTheirFlash()
    {
        const uint say = 45, action = 46, other = 44;
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (other, 9, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "never" })),
            (say, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (action, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [say, action, other], 100))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 6, 10, 6, 11);

        f.Owned(() => rider.OnChat(0, "tg", false));
        f.Advance(1000);

        Assert.Equal("0", f.Items[say].LegacyDataString);
        Assert.Equal("0", f.Items[action].LegacyDataString);
        // A box of another stack shows one change.
        Assert.Equal("1", f.Items[other].LegacyDataString);
    }
}
