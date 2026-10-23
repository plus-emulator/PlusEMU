using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A message shown by a stack that a say reached through Send Signal comes after the echo of the say, in the same millisecond.
    // A message shown by a stack the say runs itself still comes before the echo.
    [Theory]
    [InlineData(1, 0, new[] { "direct", "sg", "reply" })]
    [InlineData(0, 0, new[] { "direct", "sg", "reply" })]
    [InlineData(1, 1, new[] { "direct", "sg", "reply" })]
    [InlineData(0, 1, new[] { "direct", "sg", "reply" })]
    public void AMessageFromASignalStackFollowsTheEchoOfTheSay(int toRoom, int hideSay, string[] expected)
    {
        var (f, speaker) = SignalReplyRoom(toRoom, hideSay, 0);
        var packets = CapturePublication(speaker);

        f.Owned(() => speaker.OnChat(0, "sg", false));

        Assert.Equal(expected, ReadSpeech(packets));
        packets.Clear();
        f.Advance(1000);
        Assert.Empty(ReadSpeech(packets));
    }

    // The same reply with a delay of one step (500 ms) is not held for the echo: it goes out when its delay ends.
    [Fact]
    public void ADelayedMessageFromASignalStackStillWaitsForItsDelay()
    {
        var (f, speaker) = SignalReplyRoom(1, 0, 1);
        var packets = CapturePublication(speaker);

        f.Owned(() => speaker.OnChat(0, "sg", false));
        Assert.Equal(["direct", "sg"], ReadSpeech(packets));
        f.Advance(1000);
        Assert.Equal(["direct", "sg", "reply"], ReadSpeech(packets));
    }

    private static string[] ReadSpeech(List<byte[]> packets) => [.. packets.Where(IsChat).Select(bytes =>
    {
        var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10));

        return Encoding.UTF8.GetString(bytes, 12, length);
    })];

    // One say runs a stack of its own that shows "direct" and a stack that sends a signal; the stack that receives it shows "reply".
    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Speaker) SignalReplyRoom(int toRoom, int hideSay, int replyDelay)
    {
        const uint antenna = 61;
        string Message(string text, int delay, int shownToRoom) => "native:" + JsonSerializer.Serialize(new
        {
            Version = 2,
            Category = 1,
            NativeCode = 7,
            OwnedIntParams = new[] { shownToRoom, 34, -1 },
            Text = text,
            PrimaryItems = Array.Empty<int>(),
            SecondaryItems = Array.Empty<int>(),
            FurniSourceTypes = Array.Empty<int>(),
            UserSourceTypes = new[] { 0 },
            VariableIds = Array.Empty<int>(),
            Delay = delay,
            Quantifier = (int?)null,
            Filter = (bool?)null,
            Inverse = (bool?)null,
            SavedState = new { Snapshots = Array.Empty<int>() }
        });
        var f = new Fixture(
        [
            (70, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, hideSay, 0], Text = "sg" })),
            (71, 4, 9, 0.65, 0, "wf_act_send_signal", SignalSender(antenna)),
            (72, 5, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration { IntParams = [1, hideSay, 0], Text = "sg" })),
            (73, 5, 9, 0.65, 0, "wf_act_show_message", Message("direct", 0, toRoom)),
            (antenna, 9, 13, 0.0, 0, "wf_antenna2", null),
            (80, 6, 9, 0.0, 0, "wf_trg_recv_signal", SignalReceiver(antenna)),
            (81, 6, 9, 0.65, 0, "wf_act_show_message", Message("reply", replyDelay, toRoom))
        ], live: true);
        var speaker = f.Walker(1, 6, 12, 6, 13);
        f.Advance(1000);

        return (f, speaker);
    }
}
