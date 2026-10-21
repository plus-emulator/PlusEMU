using Xunit;

namespace Plus.Tests;

// Condition, trigger, action and add-on saves as Habbo stores them: odd values are reset or clamped, missing values take the frame
// default, and extra values are dropped. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredSaveLeniencyBoxesTests
{
    [Theory]
    [InlineData("wf_cnd_has_handitem")]
    [InlineData("wf_cnd_not_has_handitem")]
    public void HasHandItemStoresAnyHandItemAsSent(string name)
    {
        foreach (var item in new[] { int.MinValue, -100, -1, 0, 1, 2, 28, 1129, 100000, int.MaxValue }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [item]);

            Assert.True(result.Accepted, $"{name} {item}: {result}");
            Assert.Equal([item], result.Ints);
        }
    }

    [Theory]
    [InlineData("wf_cnd_has_handitem")]
    [InlineData("wf_cnd_not_has_handitem")]
    public void HasHandItemFitsItsOneInt(string name)
    {
        using var save = new WiredRawSave(name);

        Assert.Equal([2], save.Save(ints: []).Ints);
        Assert.Equal([3], save.Save(ints: [3, 4]).Ints);
    }

    [Theory]
    [InlineData("wf_trg_bot_reached_avtr")]
    [InlineData("wf_trg_bot_reached_stf")]
    public void BotReachesTriggersDropIntsAndKeepABotNameOfAnyLength(string name)
    {
        foreach (var botName in new[] { "", "Robbie", new string('a', 32), new string('a', 33), new string('a', 100), "<b>x</b>" }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [1, 2], text: botName);

            Assert.True(result.Accepted, $"{name} '{botName}': {result}");
            Assert.Empty(result.Ints);
            Assert.Equal(botName, result.Text);
        }
    }

    [Fact]
    public void StateTriggerStoresTheCurrentStateModeOnlyForOneAndDropsTheText()
    {
        const string name = "wf_trg_stuff_state";

        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (2, 0), (3, 0), (-1, 0), (100, 0), (int.MaxValue, 0) }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [sent], text: "abc");

            Assert.True(result.Accepted, $"{name} {sent}: {result}");
            Assert.Equal([stored], result.Ints);
        }

        using var fresh = new WiredRawSave(name);
        Assert.Equal([1], fresh.Save(ints: []).Ints);
        Assert.Equal([1], fresh.Save(ints: [1, 0]).Ints);
    }

    [Theory]
    [InlineData("wf_act_move_furni_as_group", new[] { 0, 0 }, 200, new[] { 0 })]
    [InlineData("wf_act_neg_send_signal", new[] { 201, 201 }, 0, new[] { 200 })]
    [InlineData("wf_trg_bot_reached_avtr", new int[0], 200, new[] { 100 })]
    [InlineData("wf_trg_bot_reached_stf", new[] { 100 }, 200, new[] { 100 })]
    public void AnEmptyUserSourceListTakesTheFrameDefault(string name, int[] furni, int firstUser, int[] expected)
    {
        using var save = new WiredRawSave(name);
        var first = save.Save(furni: furni, users: [firstUser]);
        var result = save.Save(furni: furni, users: []);

        Assert.True(result.Accepted, $"{name}: {result} after {first}");
        Assert.Equal(expected, result.Users);
    }

    [Fact]
    public void FurniNameOutputReadsTheShowMultipleFlagAsZeroOrOne()
    {
        const string name = "wf_xtra_text_output_furni_name";

        foreach (var (sent, stored) in new[] { (0, 0), (1, 1), (2, 1), (-1, 1), (100, 1) }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(ints: [sent], text: "fn\t,");

            Assert.True(result.Accepted, $"{name} {sent}: {result}");
            Assert.Equal([stored], result.Ints);
        }

        using var empty = new WiredRawSave(name);
        var none = empty.Save(ints: [], text: "fn");
        Assert.True(none.Accepted, none.ToString());
        Assert.Equal([0], none.Ints);
    }
}
