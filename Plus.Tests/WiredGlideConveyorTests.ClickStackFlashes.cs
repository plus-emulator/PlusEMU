using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint ClickTrigger = 100, ClickCondition = 101, ClickShow = 102;

    // Observed on AIR WIN63-202610201032-107964540: a User Clicks Furni stack fires on every click, also when the clicks come 100 ms apart
    // (27 of 27 clicks for each gap from 100 to 2500 ms), while its Show Message box flashes at most once in each half second window of
    // the room clock: three clicks 100 ms apart that fall in one window flash it once, and three clicks 500 ms apart, one in each window, flash it three times.
    [Theory]
    [InlineData(100, 1)]
    [InlineData(500, 3)]
    public void EveryClickFiresTheStackAndTheBoxFlashesOncePerHalfSecond(int gap, int flashes)
    {
        var (f, rider, subject) = ClickStackRoom([true]);
        var frames = CaptureFlashFrames(rider);

        for (var click = 0; click < 3; click++) {
            ObservedClickSubject(f, rider, subject);
            f.Advance(gap);
        }

        f.Advance(500);

        Assert.Equal(3, frames.Count(frame => frame.Header == ServerPacketHeader.WhisperComposer));
        Assert.Equal(flashes, frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer)
            .Count(frame => ReadBatch(frame).Any(entry => entry.Id == ClickShow)));
    }

    // Observed on AIR WIN63-202610201032-107964540: when one click fires several stacks, the boxes of every stack that passed and the
    // trigger of every stack whose condition failed flash in one ObjectsDataUpdate. The condition and the message of a failed stack do not flash. A click that only fails flashes its trigger in one packet.
    [Theory]
    [InlineData("pass,fail", new uint[] { 100, 101, 102, 110 })]
    [InlineData("pass,pass,fail", new uint[] { 100, 101, 102, 110, 111, 112, 120 })]
    [InlineData("fail", new uint[] { 100 })]
    [InlineData("pass", new uint[] { 100, 101, 102 })]
    public void OneClickFlashesEveryFiredStackInOnePacket(string stacks, uint[] flashed)
    {
        var (f, rider, subject) = ClickStackRoom([.. stacks.Split(',').Select(kind => kind == "pass")]);
        var frames = CaptureFlashFrames(rider);

        ObservedClickSubject(f, rider, subject);
        f.Advance(500);

        var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal(flashed.Order(), ReadBatch(batch).Select(entry => entry.Id).Order());
        Assert.Equal(stacks.Split(',').Count(kind => kind == "pass"), frames.Count(frame => frame.Header == ServerPacketHeader.WhisperComposer));
    }

    private static (Fixture Fixture, RoomUser Rider, Plus.HabboHotel.Items.Item Subject) ClickStackRoom(bool[] passes)
    {
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null)
        };

        for (var stack = 0; stack < passes.Length; stack++) {
            var id = ClickTrigger + (uint)(10 * stack);
            int[] inRange = passes[stack] ? [1, 1, 0] : [10, 20, 0];
            layout.Add((id, 4 + stack, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)));
            layout.Add((id + 1, 4 + stack, 12, 0.65, 0, "wf_cnd_user_count_in", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [.. inRange] })));
            layout.Add((id + 2, 4 + stack, 12, 1.3, 0, "wf_act_show_message", ShownMessage("shown")));
        }

        var f = new Fixture([.. layout], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);

        return (f, rider, f.Items[ObservedSubjectId]);
    }

    private static List<(uint Header, byte[] Payload)> CaptureFlashFrames(RoomUser user)
    {
        var frames = new List<(uint, byte[])>();
        user.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        return frames;
    }
}
