using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A save that sends more than 20 ids in a pick list gets no answer at all, and the box keeps its old picks. The count is of the
// ids sent: on Toggle State 21 ids with a repeat (20 different ones) are dropped and 20 ids that are only 10 different ones are
// saved. A repeated id saves once. The limit is per list: Furni To Furni saves 20 picks with 20 second picks and drops 21 in
// either list; Move Furni As Group drops 21 second picks.
public sealed partial class WiredGlideConveyorTests
{
    private const uint LimitBox = 90;

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) LimitFixture(string card)
    {
        var f = new Fixture(
        [
            .. Enumerable.Range(1, 40).Select(id => ((uint)id, 1 + (id - 1) % 14, 12 + (id - 1) / 14, 0.0, 0, "wf_colortile", (string?)null))
        ], live: true);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.PlaceWired(LimitBox, 4, 9, 0.65, card);
        f.Items[LimitBox].Attach(f.Room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);

        foreach (var tile in Enumerable.Range(1, 40)) {
            f.Items[(uint)tile].ExtraData = new LegacyDataFormat { Data = "0" };
        }

        var owner = f.Walker(1, 6, 10, 6, 11);

        return (f, owner, CaptureFrames(owner));
    }

    // The client sends an action save as: item, ints, text, picks, delay, furni sources, user sources, variables, secondary picks.
    private static string SaveLists(string card, int[] primary, int[] secondary)
    {
        var (f, owner, frames) = LimitFixture(card);
        var condition = card.StartsWith("wf_cnd_", StringComparison.Ordinal);
        object[] ints = card == "wf_act_toggle_state" ? [1, 0] : [0];
        object[] body = [(int)LimitBox, .. ints, "", primary.Length, .. primary.Cast<object>(), 0, 1, 100, 0, 0, secondary.Length, .. secondary.Cast<object>()];

        if (condition) {
            body = [(int)LimitBox, 1, 0, "", primary.Length, .. primary.Cast<object>(), 0, 1, 100, 0, 0, secondary.Length, .. secondary.Cast<object>()];
            Send(f, owner, frames, new SaveWiredConditionConfigEvent(MatchService()), body);
        }
        else {
            Send(f, owner, frames, new SaveWiredEffectConfigEvent(MatchService()), body);
        }

        return SaveAnswer(frames);
    }

    private static int[] Ids(int count) => [.. Enumerable.Range(1, count)];

    [Fact]
    public void TwentyPicksSaveAndMoreAreDroppedOnToggleState()
    {
        Assert.Equal("ok", SaveLists("wf_act_toggle_state", Ids(20), []));
        Assert.Equal("no answer", SaveLists("wf_act_toggle_state", Ids(21), []));
        Assert.Equal("no answer", SaveLists("wf_act_toggle_state", Ids(40), []));
    }

    [Fact]
    public void FurniToFurniDropsASaveOfTwentyOnePicks()
    {
        Assert.Equal("no answer", SaveLists("wf_act_furni_to_furni", Ids(21), Ids(5)));
    }

    [Fact]
    public void AConditionDropsASaveOfTwentyOnePicks()
    {
        Assert.Equal("no answer", SaveLists("wf_cnd_has_furni_on", Ids(21), []));
    }

    [Fact]
    public void TheLimitCountsTheIdsSentNotTheDifferentOnes()
    {
        // 21 ids, 20 different ones.
        Assert.Equal("no answer", SaveLists("wf_act_toggle_state", [.. Ids(20), 1], []));
        // 20 ids, 10 different ones.
        Assert.Equal("ok", SaveLists("wf_act_toggle_state", [.. Ids(10), .. Ids(10)], []));
    }

    [Fact]
    public void EachPickListOfFurniToFurniHasItsOwnLimitOfTwenty()
    {
        const string card = "wf_act_furni_to_furni";

        Assert.Equal("ok", SaveLists(card, Ids(20), Ids(20)));
        Assert.Equal("no answer", SaveLists(card, Ids(5), Ids(21)));
        Assert.Equal("no answer", SaveLists(card, Ids(20), Ids(21)));
        Assert.Equal("no answer", SaveLists(card, Ids(5), [.. Ids(20), 1]));
    }

    [Fact]
    public void TheSecondListOfMoveFurniAsGroupIsLimitedToTwenty()
    {
        Assert.Equal("no answer", SaveLists("wf_act_move_furni_as_group", Ids(5), Ids(21)));
    }

    // A dropped save leaves the box with the picks it had.
    [Fact]
    public void ADroppedSaveKeepsTheOldPicks()
    {
        var (f, owner, frames) = LimitFixture("wf_act_toggle_state");

        SavePicks(f, owner, frames, LimitBox, [0], 3, 5);
        SavePicks(f, owner, frames, LimitBox, [0], Ids(24));

        Assert.Equal("no answer", SaveAnswer(frames));
        Assert.Equal([3u, 5u], ((IWiredConfiguredItem)f.Box(LimitBox)).Configuration.SelectedItems.Order().ToArray());
    }

    // Five ids with one repeat save, and the box holds each id once.
    [Fact]
    public void ARepeatedIdSavesOnceAndKeepsEachIdOnce()
    {
        var (f, owner, frames) = LimitFixture("wf_act_toggle_state");
        int[] sent = [3, 5, 3, 7, 9];

        SavePicks(f, owner, frames, LimitBox, [0], sent);

        Assert.Equal("ok", SaveAnswer(frames));
        Assert.Equal([3u, 5u, 7u, 9u], ((IWiredConfiguredItem)f.Box(LimitBox)).Configuration.SelectedItems.Order().ToArray());
    }

    private static string SaveWithSources(int[] picks, int[] furniSources, int[] userSources)
    {
        var (f, owner, frames) = LimitFixture("wf_act_toggle_state");
        object[] body = [(int)LimitBox, 1, 0, "", picks.Length, .. picks.Cast<object>(), 0,
            furniSources.Length, .. furniSources.Cast<object>(), userSources.Length, .. userSources.Cast<object>(), 0, 0];

        Send(f, owner, frames, new SaveWiredEffectConfigEvent(MatchService()), body);

        return SaveAnswer(frames);
    }

    // The two source count refusals are answered even when the save has more than 20 picks.
    [Theory]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(22)]
    public void ThreeUserSourcesGetTheCountKeyAlsoWithMoreThanTwentyPicks(int picks) =>
        Assert.Equal("refused: wiredfurni.error.invalid_user_source_count", SaveWithSources(Ids(picks), [100], [0, 0, 0]));

    [Theory]
    [InlineData(5)]
    [InlineData(21)]
    public void ThreeFurniSourcesGetTheCountKeyAlsoWithMoreThanTwentyPicks(int picks) =>
        Assert.Equal("refused: wiredfurni.error.invalid_furni_source_count", SaveWithSources(Ids(picks), [100, 100, 100], []));

    // An unknown source id stays a silent drop.
    [Theory]
    [InlineData(5, "no answer")]
    [InlineData(21, "no answer")]
    public void AnUnknownUserSourceIsDroppedWithoutAnswer(int picks, string answer) =>
        Assert.Equal(answer, SaveWithSources(Ids(picks), [100], [77]));

    [Theory]
    [InlineData(5, "no answer")]
    [InlineData(21, "no answer")]
    public void AnUnknownFurniSourceIsDroppedWithoutAnswer(int picks, string answer) =>
        Assert.Equal(answer, SaveWithSources(Ids(picks), [7], []));
}
