using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Xunit;

namespace Plus.Tests;

public sealed class HeightMapUpdateIdTests
{
    // Observed on AIR WIN63-202610151745-318700621: a placement sends HeightMapUpdate 853 (package_71/class_3258), count byte then
    // x byte, y byte and short height per tile, e.g. 01 08 0A 8000.
    [Fact]
    public void HeightMapUpdateUsesTheAirIdAndLayout()
    {
        var composer = new HeightMapUpdateComposer([new(8, 10, unchecked((short)0x8000))]);
        Assert.Equal(3841u, composer.MessageId);
        Assert.Equal(new byte[] { 1, 8, 10, 0x80, 0 }, TypedFurnitureStateTests.Payload(composer));
    }
}
