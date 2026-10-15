using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Official hhus WIN63-202610131318 batch r83-logs: Write to Logs and its negative form in a stack whose condition fails or passes.
public sealed partial class WiredGlideConveyorTests
{
    private static Fixture ObservedLogRoom(int[] conditionInts)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (ObservedTriggerId, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "alpha" })),
            (ObservedActionId, 4, 12, 0.65, 0, "wf_cnd_user_count_in", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [.. conditionInts] })),
            (43, 4, 12, 0.7, 0, "wf_act_neg_log", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 0], Text = "neglog alpha" })),
            (44, 4, 12, 0.75, 0, "wf_act_log", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [0, 0], Text = "poslog alpha" }))
        };

        return new(layout, live: true);
    }

    // Official: with the condition failing, only the negative log wrote (source 1, level 0) and the positive log in the same stack
    // stayed silent; the negative box flipped and the condition and the positive log did not. With the condition passing it is the other way round.
    [Theory]
    [InlineData(false, "neglog alpha", "poslog alpha")]
    [InlineData(true, "poslog alpha", "neglog alpha")]
    public void NegativeLogWritesOnlyWhileTheConditionFails(bool conditionPasses, string written, string silent)
    {
        var f = ObservedLogRoom(conditionPasses ? [1, 1, 0] : [10, 20, 0]);
        var speaker = f.Walker(1, 4, 10, 4, 11);

        f.Owned(() => speaker.OnChat(0, "alpha", false));
        f.Advance(1500);

        var entry = Assert.Single(f.Room.GetWired().ReadLogs(0, 50).Entries);
        Assert.Equal(written, entry.Message);
        Assert.Equal((0, 1), (entry.Level, entry.WireSource));
        Assert.Empty(f.Room.GetWired().ReadLogs(0, 50, query: silent).Entries);
        // The filter menu offers System (0) and Wired (1): the line sits under Wired only.
        Assert.Single(f.Room.GetWired().ReadLogs(0, 50, source: 1).Entries);
        Assert.Empty(f.Room.GetWired().ReadLogs(0, 50, source: 0).Entries);
    }
}
