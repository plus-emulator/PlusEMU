using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const int AdjustIncrease = 0;
    private const int AdjustDecrease = 1;
    private const int AdjustSet = 2;

    // Observed on AIR WIN63-202610201032-107964540: without Execute in Order, an Adjust Clock Set runs after the Increase and Decrease
    // of the same firing, whatever the box order (18 of 18). With Execute in Order the boxes run bottom to top.
    [Theory]
    [InlineData(AdjustSet, 2, AdjustDecrease, 1, false, "120")]
    [InlineData(AdjustDecrease, 1, AdjustSet, 2, false, "120")]
    [InlineData(AdjustSet, 2, AdjustIncrease, 1, false, "120")]
    [InlineData(AdjustSet, 2, AdjustDecrease, 1, true, "60")]
    [InlineData(AdjustDecrease, 1, AdjustSet, 2, true, "120")]
    public void AdjustClockSetRunsAfterIncreaseAndDecreaseUnlessExecuteInOrder(int lowerOperation, int lowerMinutes, int upperOperation,
        int upperMinutes, bool inOrder, string expected)
    {
        for (var seed = 0; seed < 16; seed++) {
            Assert.Equal([expected, expected], FireAdjustClockPair(seed, inOrder, (lowerOperation, lowerMinutes), (upperOperation, upperMinutes)));
        }
    }

    // Observed on AIR WIN63-202610201032-107964540: two Sets in one firing still run in a random order (120, 120, 60, 60).
    [Fact]
    public void TwoAdjustClockSetsKeepARandomOrder()
    {
        var results = Enumerable.Range(0, 16).SelectMany(seed => FireAdjustClockPair(seed, false, (AdjustSet, 2), (AdjustSet, 1)));

        Assert.Equal([60, 120], results.Select(int.Parse).Distinct().Order());
    }

    // Saves both boxes through the editor's save packet, then fires the stack twice and returns the counter value the client
    // received after each firing.
    private static string[] FireAdjustClockPair(int seed, bool inOrder, (int Operation, int Minutes) lower, (int Operation, int Minutes) upper)
    {
        var fresh = JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 100, 0, 0] });
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (CounterId, 8, 12, 0.0, 0, "wf_game_upcounter1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_adjust_clock", fresh),
            (23, 4, 12, 1.3, 0, "wf_act_adjust_clock", fresh)
        };

        if (inOrder) {
            layout.Add((24, 4, 12, 1.95, 0, "wf_xtra_exec_in_order", JsonSerializer.Serialize(new WiredConfiguration())));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        f.SetEngineCallback("_effectOrderRandom", new Random(seed));
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        var rider = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(rider);
        var save = new SaveWiredEffectConfigEvent(new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>()));

        foreach (var (id, (operation, minutes)) in new[] { (22, lower), (23, upper) }) {
            f.Owned(() => save.Parse(rider.GetClient()!, EditorTestSupport.Incoming(id, 4, 0, minutes, 0, operation, "", 1, CounterId, 0, 1, 100, 0,
                0, 0)).GetAwaiter().GetResult());
            Assert.Contains(frames, frame => frame.Header == ServerPacketHeader.HideWiredConfigComposer);
            frames.Clear();
        }

        var received = new List<string>();

        for (var firing = 0; firing < 2; firing++) {
            f.Owned(() => rider.OnChat(0, "pulse", false));
            f.Advance(500);
            received.Add(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).SelectMany(CounterEntries).Last().Text);
            frames.Clear();
        }

        return [.. received];
    }
}
