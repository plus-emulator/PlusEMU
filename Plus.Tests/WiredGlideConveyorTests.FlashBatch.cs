using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A stack fired outside speech flashes its trigger and effect in the same ObjectsDataUpdate as the furni state its effect
    // writes.
    [Theory]
    [InlineData("wf_trg_periodically")]
    [InlineData("wf_trg_period_short")]
    public void AFiringFlashesInTheBatchOfItsEffectsStateWrite(string timer)
    {
        var (f, rider) = TimerGridRoom(timer);
        var batches = new List<uint[]>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.Add([.. ReadBatch(frame).Select(entry => entry.Id)]);
            }

            return false;
        };

        for (var i = 0; i < 40; i++) {
            f.Advance(50);
        }

        Assert.NotEmpty(batches);
        Assert.All(batches.Where(batch => batch.Contains(42u)), batch => Assert.Contains(GridTileId, batch));
    }
}
