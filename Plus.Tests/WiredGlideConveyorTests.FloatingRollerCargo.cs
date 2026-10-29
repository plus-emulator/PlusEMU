using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Cargo that floats above a roller top slides like cargo that rests on it, also in a room without Wired.
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.4)]
    [InlineData(0.6)]
    public void ARollerCarriesCargoThatFloatsAboveItInARoomWithoutWired(double gap)
    {
        var f = new Fixture(
        [
            (70, 5, 12, 0.0, 2, "roller", null),
            (80, 5, 12, 0.65 + gap, 0, "bc_tile_1", null)
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Room.GetRoomItemHandler().SetSpeed(0);
        f.Room.GetRoomItemHandler().GotRollers = true;
        var packets = CapturePublication(rider);

        f.Advance(500);
        f.Owned(() => f.Room.GetRoomItemHandler().OnRollerCycle());

        Assert.Contains(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.SlideObjectBundleComposer);
        Assert.Equal((6, 12), (f.Items[80].GetX, f.Items[80].GetY));
        Assert.Equal(gap, f.Items[80].GetZ, 3);
    }
}
