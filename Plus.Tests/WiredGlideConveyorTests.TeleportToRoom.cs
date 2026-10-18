using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610151745-318700621: Teleport to Room glows the user with effect 234 from the firing and takes them
    // into the linked room about 950 ms later.
    [Fact]
    public void TeleportToRoomGlowsTheUserBeforeTheForward()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_teleport_to_room", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            Text = "42",
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_teleport_to_room", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);
        var wakes = new List<long>();
        var switches = 0;
        using var switchRoom = WiredRoomForwarding.UseRoomSwitch(_ => switches++);
        f.SetEngineCallback("<ActionDue>k__BackingField", (Action<long>)wakes.Add);
        bool Forwarded() => switches > 0;

        var firedAt = f.Engine.Now;
        f.Owned(() => rider.OnChat(0, "pulse", false));

        // The forward wakes the room at its own due time instead of waiting for the next 50 ms step.
        Assert.Contains(firedAt + 950, wakes);
        f.Advance(200);
        Assert.Equal(234, rider.CurrentEffect);
        Assert.False(Forwarded());

        f.Advance(650);
        Assert.False(Forwarded());
        f.Advance(150);
        Assert.True(Forwarded());
        Assert.DoesNotContain(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.RoomForwardComposer);
        Assert.Equal(0, rider.GetClient()!.GetHabbo()!.ForwardArrivalRestoreEffect);

        // A user the client did not take away loses the glow later.
        Assert.Equal(234, rider.CurrentEffect);
        f.Advance(2500);
        Assert.Equal(0, rider.CurrentEffect);
    }
}
