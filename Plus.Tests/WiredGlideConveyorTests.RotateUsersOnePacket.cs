using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: one Move and Rotate User box that turns two users sends one WiredMovements
    // with one direction entry per user, not one packet per user.
    [Fact]
    public void RotateUserTurningTwoUsersSendsOneWiredMovementsPacket()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [-1, 9],
            FurniSourceTypes = [],
            UserSourceTypes = [WiredSources.AllRoom],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_move_rotate_user", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var other = f.Walker(2, 7, 10, 7, 11);
        rider.RotBody = rider.RotHead = 0;
        other.RotBody = other.RotHead = 2;
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "pulse", false));
        f.Advance(200);

        var movements = packets.Where(bytes => System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes)
            == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredMovementsComposer).ToArray();
        var packet = Assert.Single(movements);
        Assert.Equal([new MovementEntry(3, rider.VirtualId, 1, 1), new MovementEntry(3, other.VirtualId, 3, 3)],
            ReadMovementEntries([packet]).OrderBy(entry => entry.Id));
    }
}
