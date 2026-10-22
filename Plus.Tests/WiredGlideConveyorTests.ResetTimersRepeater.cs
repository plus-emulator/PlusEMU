using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610151745-318700621: after Reset Timers a 200 ms Periodic Short fired on the next room tick, 361 and 372 ms
    // after the reset. Observed on AIR WIN63-202610201032-107964540: a 600 s Periodic Long fired 140 ms after the reset and again 600 s
    // after that tick. Neither waited 500 ms.
    [Theory]
    [InlineData("wf_trg_period_short", 4, new[] { 2500, 2700, 2900, 3100, 3300 })]
    [InlineData("wf_trg_periodically", 1, new[] { 2500, 3000 })]
    [InlineData("wf_trg_period_long", 1, new[] { 2500 })]
    public void ARepeaterFiresOnTheFirstRoomTickAfterResetTimersAndThenEveryInterval(string timer, int units, int[] toggles)
    {
        string Config(int[] parameters, string text = "", params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            Text = text,
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (GridTileId, 8, 12, 0.0, 0, "wf_colortile", null),
            (41, 4, 12, 0.0, 0, timer, Config([units])),
            (42, 4, 12, 0.65, 0, "wf_act_toggle_state", Config([0, 100], "", GridTileId)),
            (43, 6, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "rst")),
            (44, 6, 12, 0.65, 0, "wf_act_reset_timers", Config([]))
        ], live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var now = 0;
        var resetAt = -1;
        var toggledAt = new List<int>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                var ids = ReadBatch(frame).Select(entry => entry.Id).ToArray();

                if (ids.Contains(44u) && resetAt < 0) {
                    resetAt = now;
                }

                if (ids.Contains(GridTileId)) {
                    toggledAt.Add(now);
                }
            }

            return false;
        };

        for (; now < 2150;) {
            now += 50;
            f.Advance(50);
        }

        f.Owned(() => rider.OnChat(0, "rst", false));

        for (; now < 3400;) {
            now += 50;
            f.Advance(50);
        }

        Assert.Equal(2150, resetAt);
        Assert.Equal(toggles, toggledAt.Where(at => at > resetAt));
    }
}
