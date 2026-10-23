using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A say trigger with a relative move (+1x) and a match back to the saved tile, both in one firing. Without Execute in Order the
// furni ends where it started and gets no movement entry; with it, one entry.
public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NetZeroFurniWithoutExecuteInOrderPublishesNoMovement(bool moveFirst)
    {
        var moves = RunReturnPair(moveFirst, inOrder: false, out var x);

        Assert.Empty(moves.Where(move => move.Type == 1));
        Assert.Equal(9, x);
    }

    [Fact]
    public void NetZeroFurniWithExecuteInOrderPublishesOneEntryFromAndToTheSameTile()
    {
        var moves = RunReturnPair(moveFirst: true, inOrder: true, out var x);

        var entry = Assert.Single(moves.Where(move => move.Type == 1));
        Assert.Equal((9, 9), (entry.FromX, entry.ToX));
        Assert.Equal(9, x);
    }

    [Fact]
    public void MovedFurniWithExecuteInOrderStillPublishesItsRealMove()
    {
        var moves = RunReturnPair(moveFirst: false, inOrder: true, out var x);

        var entry = Assert.Single(moves.Where(move => move.Type == 1));
        Assert.Equal((9, 10), (entry.FromX, entry.ToX));
        Assert.Equal(10, x);
    }

    private static List<PublishedMove> RunReturnPair(bool moveFirst, bool inOrder, out int x)
    {
        var move = ("wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [1, 0], [ObservedSubjectId], 100));
        var back = ("wf_act_match_to_sshot", JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [0, 0, 1, 1, 100],
            SelectedItems = [ObservedSubjectId],
            Snapshots = [new(ObservedSubjectId, 29284, 9, 9, 0, 0, "")]
        }));
        var lower = moveFirst ? move : back;
        var upper = moveFirst ? back : move;
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, lower.Item1, lower.Item2),
            (ObservedActionId + 1, 4, 12, 1.3, 0, upper.Item1, upper.Item2)
        };

        if (inOrder) {
            layout.Add((ObservedActionId + 2, 4, 12, 1.95, 0, "wf_xtra_exec_in_order", JsonSerializer.Serialize(new WiredConfiguration())));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "alpha", false));
        f.Advance(500);

        x = f.Items[ObservedSubjectId].GetX;

        return ReadPublication(packets);
    }
}
