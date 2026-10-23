using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint MarkerId = 80;

    // A signal split over two users fires the receiver twice in one tick, and each firing sends the marker to its user. The tick
    // sends one WiredMovements with one entry for the marker, from where it was at the start of the tick to where it ends.
    [Fact]
    public void TwoReceiverFiringsInOneTickSendOneEntryFromTheStartToTheEnd()
    {
        var (f, speaker) = SplitSignalRoom(8, 10);
        var packets = CapturePublication(speaker);

        f.Owned(() => speaker.OnChat(0, "u3go", false));
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        var entry = Assert.Single(batch);
        Assert.Equal((1, (int)MarkerId, 8, 9), (entry.Type, entry.Id, entry.FromX, entry.ToX));
        Assert.Equal((9, 12), (f.Items[MarkerId].GetX, f.Items[MarkerId].GetY));
    }

    // When the two firings move the marker away and back, the tick still sends one entry, from its tile to the same tile.
    [Fact]
    public void AFurniMovedAwayAndBackInOneTickKeepsOneEntry()
    {
        var (f, speaker) = SplitSignalRoom(9, 12);
        var packets = CapturePublication(speaker);

        f.Owned(() => speaker.OnChat(0, "u3go", false));
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        var entry = Assert.Single(batch);
        Assert.Equal((1, (int)MarkerId, 9, 9), (entry.Type, entry.Id, entry.FromX, entry.ToX));
        Assert.Equal((9, 12), (f.Items[MarkerId].GetX, f.Items[MarkerId].GetY));
    }

    // A say selects the users on (6,12) and (9,12) and sends a signal split by user. The receiver sends the marker to the
    // signalled user. Send Signal shuffles the users, so the box keeps them in selection order here.
    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Speaker) SplitSignalRoom(int markerX, int markerY)
    {
        const uint antenna = 81;
        Assert.True(WiredBoxRegistry.TryGet("wf_act_send_signal", out var signal));
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_users_area", out var area));
        Assert.True(WiredBoxRegistry.TryGet("wf_act_furni_to_user", out var toUser));
        var f = new Fixture(
        [
            (MarkerId, markerX, markerY, 0.0, 0, "bc_tile_1", null),
            (antenna, 11, 9, 0.0, 0, "wf_antenna2", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("u3go")),
            (84, 4, 14, 0.65, 0, "wf_slc_users_area", "native:" + JsonSerializer.Serialize(WiredNativeEditorProjection.DefaultNative(area) with { OwnedIntParams = [6, 12, 4, 1] })),
            (85, 4, 14, 1.3, 0, "wf_act_send_signal", "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
            {
                Category = signal.Category,
                NativeCode = signal.EditorCode,
                OwnedIntParams = [0, 1],
                PrimaryItems = [new WiredNativeItemReference(antenna, false)],
                FurniSourceTypes = [100, 200],
                UserSourceTypes = [200],
                Delay = 0
            })),
            (86, 5, 14, 0.0, 0, "wf_trg_recv_signal", SignalReceiver(antenna)),
            (87, 5, 14, 0.65, 0, "wf_act_furni_to_user", "native:" + JsonSerializer.Serialize(WiredNativeEditorProjection.DefaultNative(toUser) with
            {
                PrimaryItems = [new WiredNativeItemReference(MarkerId, false)],
                FurniSourceTypes = [100],
                UserSourceTypes = [201]
            }))
        ], live: true);
        var speaker = f.Walker(1, 6, 11, 6, 12);
        f.Walker(2, 9, 11, 9, 12);
        f.SetBoxField(85, "_random", new KeepOrderRandom());
        f.Advance(1000);

        return (f, speaker);
    }

    private sealed class KeepOrderRandom : Random
    {
        public override int Next(int minValue, int maxValue) => minValue;
    }
}
