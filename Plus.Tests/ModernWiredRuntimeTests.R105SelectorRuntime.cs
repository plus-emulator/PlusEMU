using Xunit;

namespace Plus.Tests;

// What each selector picks in a real firing. Observed on AIR WIN63-202610201032-107964540.
public partial class ModernWiredRuntimeTests
{
    private static string PickedFurni(params uint[] ids) =>
        Row(1, "[]", false, false, picks: "[" + string.Join(",", ids.Select(id => $$"""{"ItemId":{{id}},"Wall":false}""")) + "]");

    // Users By Type is not a bit mask: 1 Habbo, 2 Pet, 4 Bot, and 3 matches nobody.
    [Theory]
    [InlineData(1, new[] { 7, 8 })]
    [InlineData(2, new int[0])]
    [InlineData(3, new int[0])]
    [InlineData(4, new[] { 9 })]
    [InlineData(5, new[] { 9 })]
    [InlineData(0, new[] { 7, 8 })]
    public void UsersByTypeSelectsOneKindOfUser(int type, int[] expected)
    {
        using var s = new SelectorStack();
        AddNamedBot(s.F, 9, "Robbie", 2, 1);
        s.Selector("wf_slc_users_bytype", Row(2, $"[{type}]", false, false));

        Assert.Equal(expected, s.Users(s.Fire()));
    }

    [Fact]
    public void UsersByTypeThreeWithInvertSelectsEveryone()
    {
        using var s = new SelectorStack();
        AddNamedBot(s.F, 9, "Robbie", 2, 1);
        s.Selector("wf_slc_users_bytype", Row(2, "[3]", false, true));

        Assert.Equal([7, 8, 9], s.Users(s.Fire()));
    }

    // Three tiles stacked 0.2 high each: their heights are 0.2, 0.4 and 0.6, the last one summed as 0.2 + 0.2 + 0.2 in doubles.
    [Theory]
    [InlineData(60, 1, new uint[] { 403 })]
    [InlineData(40, 3, new uint[] { 401, 402 })]
    [InlineData(40, 4, new uint[] { 401, 403 })]
    [InlineData(40, 5, new uint[] { 402, 403 })]
    [InlineData(60, 3, new uint[] { 401, 402, 403 })]
    [InlineData(60, 2, new uint[0])]
    [InlineData(60, 0, new uint[] { 401, 402 })]
    public void AltitudeComparesTheBottomHeightWithoutAFloatError(int altitude, int comparison, uint[] expected)
    {
        using var s = new SelectorStack();
        var height = 0.0;

        foreach (var id in new uint[] { 401, 402, 403 }) {
            height += 0.2;
            s.Place(id, "block", 1, 1).SetState(1, 1, height);
        }

        s.Selector("wf_slc_furni_picks", PickedFurni(401, 402, 403));
        s.Selector("wf_slc_furni_altitude", Row(16, $"[{altitude},{comparison}]", true, false));

        Assert.Equal(expected, s.Furni(s.Fire()));
    }

    // No group has these ids, so nobody is selected; invert selects every user.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("99999999999999")]
    public void AGroupIdOfNoGroupSelectsNobody(string group)
    {
        using (var s = new SelectorStack()) {
            s.Selector("wf_slc_users_group", Row(15, "[]", false, false, text: group));
            Assert.Empty(s.Users(s.Fire()));
        }

        using (var s = new SelectorStack()) {
            s.Selector("wf_slc_users_group", Row(15, "[]", false, true, text: group));
            Assert.Equal([7, 8], s.Users(s.Fire()));
        }
    }

    // An eighteen-int neighborhood selects the same window as the old seventeen-int one.
    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    public void ANeighborhoodRowOfEitherLengthSelectsTheSameWindow(int length)
    {
        using var s = new SelectorStack();
        int[] mask = [0, 0, 0, 511, .. Enumerable.Repeat(0, length - 4)];
        s.Selector("wf_slc_furni_neighborhood", Row(6, $"[{string.Join(",", mask)}]", false, false, furni: "[100]", users: "[0]",
            picks: Pick(202)));

        Assert.Equal(new uint[] { 201, 202, 203 }, s.Furni(s.Fire()).Where(id => id is >= 201 and <= 203));
    }
}
