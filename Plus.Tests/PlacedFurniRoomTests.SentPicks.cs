using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A save counts every id it sends. A list that repeats an id saves when it sends at most 20 ids and gets no answer from 21 ids on.
// The reopened order follows a set built from the different ids and sized from the number of ids sent (repeats counted).
// An id that names no furni is dropped without an answer and counts as sent too.
// The ids below are Builders Club ids (2147418112 plus nn) moved to ids in the same buckets (576 + (nn xor 63)), which keep every order in 32 or 64 buckets.
public partial class PlacedFurniRoomTests
{
    // An id that names no furni (nn from 1500) moves to an id far from the furni of the room.
    private static int SentId(int nn) => nn >= 1500 ? 9000 + nn - 1500 : 576 + (nn ^ 63) - (int)SecondListBase;

    private static int[] SentIds(params int[] nn) => [.. nn.Select(SentId)];

    // Saves: the ids sent (nn) and the order the box reopened them in (nn).
    private static readonly (int[] Sent, int[] Reopened)[] RepeatedSaves =
    [
        ([14, 14], [14]),
        ([27, 17, 27, 29, 17], [29, 27, 17]),
        ([24, 14, 28, 16, 18, 6, 5, 26, 15, 20, 24, 14, 28, 16, 18, 6, 5, 26, 15, 20], [15, 14, 28, 26, 24, 6, 5, 20, 18, 16]),
        ([27, 18, 16, 5, 0, 7, 22, 24, 1, 17, 25, 25], [27, 25, 24, 7, 22, 5, 18, 17, 1, 16, 0]),
        ([4, 21, 22, 4, 22, 4, 10, 22, 22, 21, 15, 25, 1, 11, 26, 2, 10, 1, 26, 25], [15, 11, 26, 10, 25, 22, 21, 4, 2, 1])
    ];

    // The same cards with 21 ids or more: no answer.
    private static readonly int[][] OversizedRepeatedSaves =
    [
        [.. Enumerable.Range(0, 20), 3],
        [.. Enumerable.Range(0, 11), .. Enumerable.Range(0, 11)],
        [.. Enumerable.Range(0, 20), .. Enumerable.Range(0, 20)]
    ];

    private static readonly string[] SentPicksCards =
    [
        "wf_var_furni", "wf_var_user", "wf_var_room", "wf_var_context",
        "wf_xtra_filter_furni", "wf_xtra_filter_furni_by_var", "wf_xtra_filter_users", "wf_xtra_filter_users_by_var", "wf_xtra_mov_curve",
        "wf_xtra_mov_physics", "wf_xtra_rotate_to_dir", "wf_xtra_random", "wf_xtra_unseen", "wf_xtra_exec_in_order", "wf_xtra_execution_limit",
        "wf_xtra_mov_carry_users", "wf_xtra_mov_no_animation", "wf_xtra_anim_time", "wf_xtra_var_time_util", "wf_xtra_var_text_connector",
        "wf_xtra_var_share", "wf_xtra_var_import", "wf_act_toggle_state"
    ];

    public static TheoryData<string, int> RepeatedSaveRows()
    {
        var rows = new TheoryData<string, int>();

        foreach (var card in SentPicksCards) {
            for (var row = 0; row < RepeatedSaves.Length; row++) {
                rows.Add(card, row);
            }
        }

        return rows;
    }

    private void SentPicksRoom(string card)
    {
        foreach (var nn in Enumerable.Range(0, 30)) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, SecondListFurni((uint)(576 + (nn ^ 63)), "tile"), nn % 4, 3 - nn / 8 % 2, 0, true, false, false));
        }

        PlaceVariableBox(SecondListBox, card);
    }

    [Theory]
    [MemberData(nameof(RepeatedSaveRows))]
    public void ARepeatedIdSavesOnTheCardsOfTheProbeAndReopensInTheOrderOfTheSentCount(string card, int row)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();
        SentPicksRoom(card);
        var (sent, reopened) = RepeatedSaves[row];

        Assert.Equal("saved", SaveLists(service, descriptor, SentIds(sent), []));
        Assert.Equal(SentIds(reopened), ReopenLists().First);
    }

    [Theory]
    [MemberData(nameof(SentPicksCardNames))]
    public void ASaveOfMoreThanTwentyIdsRepeatsCountedGetsNoAnswerOnTheCardsOfTheProbe(string card)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();
        SentPicksRoom(card);

        foreach (var sent in OversizedRepeatedSaves) {
            Assert.Equal("dropped", SaveLists(service, descriptor, SentIds(sent), []));
        }
    }

    public static TheoryData<string> SentPicksCardNames() => [.. SentPicksCards];

    // Saves that send ids of no furni (nn from 1500), some with a repeat: the ids sent and the order the card reopened them in.
    private static readonly (int[] Sent, int[] Reopened)[] SavesWithIdsOfNoFurni =
    [
        ([1502, 5, 7, 20, 22, 4, 8, 25, 27, 26, 9, 21], [27, 26, 25, 9, 8, 7, 22, 21, 5, 20, 4]),
        ([13, 20, 8, 9, 29, 0, 23, 3, 26, 1502, 15, 26], [15, 29, 13, 26, 9, 8, 23, 20, 3, 0]),
        ([1, 4, 23, 26, 17, 24, 1505, 11, 7, 4, 6, 15, 16], [15, 11, 26, 24, 23, 7, 6, 4, 17, 1, 16]),
        ([20, 13, 6, 14, 1, 19, 8, 24, 1500, 1506, 16, 11, 10], [14, 13, 11, 10, 24, 8, 6, 20, 19, 1, 16]),
        ([11, 7, 3, 1502, 27, 10, 23, 27, 13, 24, 1, 1504, 13], [13, 27, 11, 10, 24, 23, 7, 3, 1]),
        ([21, 28, 12, 16, 15, 26, 0, 5, 1, 1507, 14, 24, 8], [15, 14, 28, 12, 26, 24, 8, 21, 5, 1, 16, 0]),
        ([1504, 23, 1505, 0, 5, 1, 8, 21, 18, 6, 27, 1503, 22], [27, 8, 23, 22, 6, 21, 5, 18, 1, 0]),
        ([11, 27, 8, 15, 1, 9, 6, 1501, 12, 4, 23, 7, 1, 1, 25], [15, 12, 27, 11, 25, 9, 8, 23, 7, 6, 4, 1])
    ];

    public static TheoryData<string, bool, int> NoFurniSaveRows()
    {
        var rows = new TheoryData<string, bool, int>();

        foreach (var (card, second) in new[] { ("wf_act_toggle_state", false), ("wf_act_toggle_state", true), ("wf_xtra_filter_furni", false) }) {
            for (var row = 0; row < SavesWithIdsOfNoFurni.Length; row++) {
                rows.Add(card, second, row);
            }
        }

        return rows;
    }

    [Theory]
    [MemberData(nameof(NoFurniSaveRows))]
    public void AnIdOfNoFurniIsDroppedAndCountsAsSentForTheOrderOfTheOthers(string card, bool secondList, int row)
    {
        Assert.True(WiredBoxRegistry.TryGet(card, out var descriptor));
        var service = UseVariableRows();
        SentPicksRoom(card);
        var (sent, reopened) = SavesWithIdsOfNoFurni[row];

        Assert.Equal("saved", SaveLists(service, descriptor, secondList ? [] : SentIds(sent), secondList ? SentIds(sent) : []));
        var lists = ReopenLists();
        Assert.Equal(SentIds(reopened), secondList ? lists.Second : lists.First);
    }
}
