using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Give Variable into a context variable from a furni variable of several picked furni gives the value of one of them, drawn at random anew
    // at every firing. One picked furni always gives its own value.
    [Theory]
    [InlineData(2, new[] { "c=21", "c=22" })]
    [InlineData(1, new[] { "c=22" })]
    public async Task AGiveIntoAContextVariableFromSeveralFurniTakesTheValueOfARandomOne(int picked, string[] expected)
    {
        const uint second = 421;
        var (wired, actor, clock) = await DelayedPointerRoom();
        var other = Furni(second, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, other, 2, 0, 0, true, false, false));
        await DpGive(DpFurni, "furni:" + DpFa, 21, picks: [DpHolder]);
        await DpGive(DpFurni, "furni:" + DpFa, 22, picks: [second]);
        uint[] picks = picked == 2 ? [DpHolder, second] : [second];
        await DpEffect("wf_act_give_var", [DpContext, 0, 0, 0, 1, DpFurni, 0], ["ctx:" + DpCt, "furni:" + DpFa], furni: [0, 100], users: [0, 0],
            picks: picks);
        await DpShow("c=$(c)", 1);
        await DpOutput("c", DpContext, "ctx:" + DpCt);
        var seen = new HashSet<string>();

        for (var firing = 0; firing < 30; firing++) {
            seen.Add(Assert.Single(DpFire(wired, actor, clock, 1)));
        }

        Assert.Equal(expected, seen.Order());
    }
}
