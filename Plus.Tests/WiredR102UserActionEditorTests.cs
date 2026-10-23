using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// User Performs Action (trigger and both conditions) as Habbo stores it.
public sealed class WiredR102UserActionEditorTests
{
    public static TheoryData<string> Names => new("wf_trg_user_performs_action", "wf_cnd_user_performs_action", "wf_cnd_not_user_performs_action");

    [Theory]
    [MemberData(nameof(Names))]
    public void ActionCodesZeroToElevenAndSixtySevenAreStoredIncludingTheUnofferedNine(string name)
    {
        foreach (var code in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 67 }) {
            Assert.Equal([code], Saved(name, [code]).Ints);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void AnUnknownActionCodeIsStoredAsZero(string name)
    {
        foreach (var code in new[] { 12, 66, 68, 99, -1 }) {
            Assert.Equal([0], Saved(name, [code]).Ints);
        }
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheFilterTextIsStoredAsSentForEveryAction(string name)
    {
        foreach (var (code, text) in new[] { (0, "5"), (6, "5"), (67, "5"), (10, "0"), (10, "17"), (10, "18"), (10, "-1"), (10, "5 "), (10, "x"),
            (10, "00"), (11, "dance 1"), (11, "dance 4"), (11, "dance 0"), (11, "dance 5"), (11, "dance"), (11, "1"), (11, "Dance 2"), (0, "dance 1") }) {
            var saved = Saved(name, [code], text: text);
            Assert.Equal(text, saved.Text);
            Assert.Equal([code], saved.Ints);
        }

        Assert.Equal("", Saved(name, [10], text: "").Text);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ExtraIntsAreDroppedAndNoIntsMeansWave(string name)
    {
        Assert.Equal([10], Saved(name, [10, 4], text: "3").Ints);
        Assert.Equal([0], Saved(name, [], text: "3").Ints);
    }

    [Theory]
    [InlineData("wf_cnd_user_performs_action")]
    [InlineData("wf_cnd_not_user_performs_action")]
    public void TheConditionsAcceptTheUserSourcesTheyOfferAndRefuseOtherWithTheUserSourceKey(string name)
    {
        foreach (var source in new[] { 0, 200, 201, 11 }) {
            Assert.Equal([source], Saved(name, [3], users: [source]).Users);
        }

        var refused = Save(name, [3], users: [100]);
        Assert.False(refused.Accepted);
        Assert.Equal("wiredfurni.error.invalid_user_source", refused.Key);
    }

    [Fact]
    public void ActionNineIsStoredButNoAvatarActionEverMatchesIt()
    {
        var runtime = Saved("wf_trg_user_performs_action", [9]).RuntimeInts;

        for (var action = 1; action <= 12; action++) {
            Assert.False(WiredTriggerPredicates.MatchesAction(new() { IntParams = [.. runtime] }, action, -1));
        }
    }

    [Fact]
    public void AFilterTheServerCannotReadLeavesTheActionUnfiltered()
    {
        var sign = new WiredConfiguration { IntParams = [.. Saved("wf_trg_user_performs_action", [10], text: "x").RuntimeInts] };
        Assert.True(WiredTriggerPredicates.MatchesAction(sign, (int)WiredAvatarAction.Sign, 5));
        Assert.False(WiredTriggerPredicates.MatchesAction(sign, (int)WiredAvatarAction.Wave, 5));
        var filtered = new WiredConfiguration { IntParams = [.. Saved("wf_trg_user_performs_action", [10], text: "3").RuntimeInts] };
        Assert.True(WiredTriggerPredicates.MatchesAction(filtered, (int)WiredAvatarAction.Sign, 3));
        Assert.False(WiredTriggerPredicates.MatchesAction(filtered, (int)WiredAvatarAction.Sign, 4));
        var wave = new WiredConfiguration { IntParams = [.. Saved("wf_trg_user_performs_action", [0], text: "5").RuntimeInts] };
        Assert.True(WiredTriggerPredicates.MatchesAction(wave, (int)WiredAvatarAction.Wave, -1));
    }

    private static Result Saved(string name, int[] ints, int[]? users = null, string? text = null)
    {
        var result = Save(name, ints, null, users, text);
        Assert.True(result.Accepted, Show(result));

        return result;
    }
}
