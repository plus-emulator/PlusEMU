using System.Text.Json;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Without Execute in Order a match_to_sshot below a toggle_state or rel_mov still runs last; with it the lower match runs first.
public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData(false, "0")]
    [InlineData(true, "1")]
    public void LowerStateSnapshotRunsAfterTheToggleUnlessExecuteInOrder(bool inOrder, string expected)
    {
        for (var seed = 0; seed < 8; seed++) {
            var subject = RunSayPair(seed, inOrder, "wf_colortile",
                ("wf_act_match_to_sshot", JsonSerializer.Serialize(new WiredConfiguration
                {
                    IntParams = [1, 0, 0, 0, 100],
                    SelectedItems = [ObservedSubjectId],
                    Snapshots = [new(ObservedSubjectId, 29284, 9, 9, 0, 0, "0")]
                })),
                ("wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [ObservedSubjectId], 100)));

            Assert.Equal(expected, subject.LegacyDataString);
        }
    }

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 10)]
    public void LowerPositionSnapshotRunsAfterTheRelativeMoveUnlessExecuteInOrder(bool inOrder, int expectedX)
    {
        for (var seed = 0; seed < 8; seed++) {
            var subject = RunSayPair(seed, inOrder, "bc_tile_1",
                ("wf_act_match_to_sshot", JsonSerializer.Serialize(new WiredConfiguration
                {
                    IntParams = [0, 0, 1, 1, 100],
                    SelectedItems = [ObservedSubjectId],
                    Snapshots = [new(ObservedSubjectId, 29284, 9, 9, 0, 0, "")]
                })),
                ("wf_act_rel_mov", ObservedNative("wf_act_rel_mov", [1, 0], [ObservedSubjectId], 100)));

            Assert.Equal((expectedX, 9), (subject.GetX, subject.GetY));
        }
    }

    private static Plus.HabboHotel.Items.Item RunSayPair(int seed, bool inOrder, string subjectName,
        (string Name, string Json) lower, (string Name, string Json) upper)
    {
        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, subjectName, null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, lower.Name, lower.Json),
            (ObservedActionId + 1, 4, 12, 1.3, 0, upper.Name, upper.Json)
        };

        if (inOrder) {
            layout.Add((ObservedActionId + 2, 4, 12, 1.95, 0, "wf_xtra_exec_in_order", JsonSerializer.Serialize(new WiredConfiguration())));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        f.SetEngineCallback("_effectOrderRandom", new Random(seed));
        var subject = f.Items[ObservedSubjectId];
        subject.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        var rider = f.Walker(1, 4, 10, 4, 11);

        f.Owned(() => rider.OnChat(0, "alpha", false));
        f.Advance(500);

        return subject;
    }
}
