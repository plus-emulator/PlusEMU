using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Move and Rotate that turns furni stacked on one tile, such as Wired boxes, sends one ObjectUpdate for each of them and no
    // WiredMovements, also for the ones that end on a new height.
    [Fact]
    public void TurningFurniStackedOnOneTileSendsObjectUpdatesAndNoWiredMovements()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var descriptor));
        var turn = "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0, 1],
            PrimaryItems = [new WiredNativeItemReference(21, false), new WiredNativeItemReference(23, false)],
            FurniSourceTypes = [100],
            Delay = 0
        });
        var f = new Fixture(
        [
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", SaysConfig("rot")),
            (22, 4, 12, 0.65, 0, "wf_act_move_rotate", turn),
            (23, 4, 12, 1.3, 0, "wf_act_show_message", null)
        ], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "rot", false));
        f.Advance(300);

        Assert.Empty(packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer));
        Assert.Equal(2, packets.Count(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.ObjectUpdateComposer));
        Assert.NotEqual(0, f.Items[21].Rotation);
        Assert.NotEqual(0, f.Items[23].Rotation);
    }
}
