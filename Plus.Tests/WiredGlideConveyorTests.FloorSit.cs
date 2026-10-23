using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A user who faces a diagonal and sits on the floor turns one step clockwise, and the head follows the body (3 -> 4, 5 -> 6,
    // 7 -> 0).
    [Theory]
    [InlineData(3, 4)]
    [InlineData(5, 6)]
    [InlineData(7, 0)]
    public async Task SittingOnTheFloorFromADiagonalTurnsOneStepClockwise(int facing, int seated)
    {
        var f = new Fixture([], live: true);
        var sitter = f.Walker(1, 6, 5, 6, 6);
        f.Owned(() =>
        {
            sitter.RotBody = facing;
            sitter.RotHead = facing;
            sitter.UpdateNeeded = true;
        });
        f.Advance(500);
        var packets = CapturePublication(sitter);
        using var manager = new PacketManager([new SitEvent(new RoomAvatarActionService(TimeProvider.System,
            CatalogSnapshotTestSupport.Proxy<IQuestManager>((_, _) => null), TestNavigationRewards.Instance))],
            NullLogger<PacketManager>.Instance);

        await manager.TryExecutePacket(sitter.GetClient()!, ClientPacketHeader.SitEvent, PosturePacket(1));
        f.Advance(500);

        var update = Assert.Single(ReadUserUpdates(packets), user => user.Index == sitter.VirtualId);
        Assert.Equal((seated, seated), (update.Head, update.Body));
        Assert.Contains("/sit 0.55 1/", update.Status);
    }

    private static FlashIncomingPacket PosturePacket(int posture)
    {
        using var stream = PlusMemoryStream.GetStream();
        new FlashOutgoingPacket(stream).WriteInteger(posture);

        return new() { Buffer = stream.ToArray().AsMemory(6) };
    }
}
