using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A stack that moves furni flashes its trigger, its add-on and the moving effect in one ObjectsDataUpdate, before the
    // WiredMovements packet.
    [Fact]
    public void AMovingStackFlashesItsTriggerAddonAndEffectInOneBatch()
    {
        const uint subject = 50, trigger = 51, mover = 52, addon = 53;
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var move));
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_anim_time", out var animation));
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_says_something", out var says));
        string Native(WiredBoxDescriptor descriptor, int[] owned, params uint[] items) => "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            Text = descriptor == says ? "pj1" : "",
            OwnedIntParams = [.. owned],
            PrimaryItems = [.. items.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = items.Length > 0 ? [100] : [],
            Delay = items.Length > 0 ? 0 : null
        });
        var f = new Fixture(
        [
            (subject, 8, 12, 0.0, 0, "bc_tile_1", null),
            (trigger, 4, 12, 0.0, 0, "wf_trg_says_something", Native(says, [0, 0, 0])),
            (mover, 4, 12, 0.65, 0, "wf_act_move_rotate", Native(move, [4, 0], subject)),
            (addon, 4, 12, 1.3, 0, "wf_xtra_anim_time", Native(animation, [200]))
        ], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var batches = new List<uint[]>();
        var movements = 0;
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.Add([.. ReadBatch(frame).Select(entry => entry.Id)]);
            }
            else if (frame.Item1 == ServerPacketHeader.WiredMovementsComposer) {
                movements++;
            }

            return false;
        };

        f.Owned(() => f.Room.GetWired().Dispatch(new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = rider, Message = "pj1" }));
        f.Advance(100);

        Assert.True(movements > 0, "the stack moved nothing");
        var flashed = batches.Where(batch => batch.Contains(mover)).ToArray();
        Assert.NotEmpty(flashed);
        Assert.All(flashed, batch => Assert.Subset(batch.ToHashSet(), new HashSet<uint> { trigger, mover, addon }));
    }
}
