using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // One click on a furni that fires a passing and a failing User Clicks Furni stack sends one ObjectsDataUpdate with the boxes
    // of every passing stack and the trigger of every failing stack, then the passing stacks' chat. A click whose stacks all
    // fail sends its trigger flash just as fast. The stacks run at the click, not on a later room pass.
    [Theory]
    [InlineData("pass,fail", new uint[] { 100, 101, 102, 110 })]
    [InlineData("pass,pass,fail", new uint[] { 100, 101, 102, 110, 111, 112, 120 })]
    [InlineData("fail", new uint[] { 100 })]
    [InlineData("pass", new uint[] { 100, 101, 102 })]
    public async Task OneClickPacketSendsEveryStackFlashInOnePacketAtTheClick(string stacks, uint[] flashed)
    {
        var passing = stacks.Split(',').Count(kind => kind == "pass");
        var (f, rider, subject) = ClickStackRoom([.. stacks.Split(',').Select(kind => kind == "pass")]);
        var frames = CaptureFlashFrames(rider);

        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!, ClickPacket((int)subject.Id, 0));

        // Sent with the click, before any room pass.
        var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal(flashed.Order(), ReadBatch(batch).Select(entry => entry.Id).Order());
        Assert.Equal(passing, frames.Count(frame => frame.Header == ServerPacketHeader.WhisperComposer));

        if (passing > 0) {
            Assert.True(frames.IndexOf(batch) < frames.FindIndex(frame => frame.Header == ServerPacketHeader.WhisperComposer));
        }

        f.Advance(1000);

        Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal(passing, frames.Count(frame => frame.Header == ServerPacketHeader.WhisperComposer));
    }

    private static FlashIncomingPacket ClickPacket(params int[] values)
    {
        using var stream = PlusMemoryStream.GetStream();
        var packet = new FlashOutgoingPacket(stream);

        foreach (var value in values) {
            packet.WriteInteger(value);
        }

        return new() { Buffer = stream.ToArray().AsMemory(6) };
    }
}
