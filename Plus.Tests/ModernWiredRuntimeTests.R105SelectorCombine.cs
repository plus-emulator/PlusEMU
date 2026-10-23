using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// How Filter existing selection and Invert combine the selectors of one stack, and how Remote Selection combines the stacks it picks. P picks the test furni and the first block, S is an area over the test furni and the two other blocks, S2 an area over the last block.
/// </summary>
public partial class ModernWiredRuntimeTests
{
    private static string FurniArea(int x, int y, int width, int length, bool filter = false, bool invert = false) =>
        Row(7, $"[{x},{y},{width},{length}]", filter, invert);

    private static string FurniPicks(uint[] ids, bool filter = false, bool invert = false) =>
        Row(1, "[]", filter, invert, picks: "[" + string.Join(",", ids.Select(id => $$"""{"ItemId":{{id}},"Wall":false}""")) + "]");

    private static string P(bool filter = false, bool invert = false) => FurniPicks([1, 201], filter, invert);
    private static string S(bool filter = false, bool invert = false) => FurniArea(1, 1, 2, 2, filter, invert);
    private static string S2(bool filter = false, bool invert = false) => FurniArea(2, 2, 1, 1, filter, invert);

    [Theory]
    [InlineData("S", new uint[] { 1, 202, 203 })]
    [InlineData("S/f", new uint[0])]
    [InlineData("S/fi", new uint[0])]
    [InlineData("P,S/f", new uint[] { 1 })]
    [InlineData("P,S/fi", new uint[] { 201 })]
    [InlineData("S,P", new uint[] { 1, 201, 202, 203 })]
    [InlineData("S,S2", new uint[] { 1, 202, 203 })]
    [InlineData("P,S/f,S2/f", new uint[0])]
    [InlineData("S,P/f", new uint[] { 1 })]
    [InlineData("S,P/fi", new uint[] { 202, 203 })]
    [InlineData("S/f,P", new uint[] { 1 })]
    public void R105SelectorOptionsCombineLikeHabbo(string stack, uint[] expected)
    {
        using var s = new SelectorStack();

        foreach (var part in stack.Split(',')) {
            var (name, flags) = (part.Split('/')[0], part.Contains('/') ? part.Split('/')[1] : "");
            var filter = flags.Contains('f');
            var invert = flags.Contains('i');
            s.Selector(name == "S2" ? "wf_slc_furni_area" : name == "S" ? "wf_slc_furni_area" : "wf_slc_furni_picks",
                name == "S2" ? S2(filter, invert) : name == "S" ? S(filter, invert) : P(filter, invert));
        }

        Assert.Equal(expected, s.Furni(s.Fire()));
    }

    [Fact]
    public void R105InvertAddsEveryOtherFurniIncludingWiredBoxes()
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_furni_picks", P());
        var inverted = s.Selector("wf_slc_furni_area", S(invert: true));

        var furni = s.Furni(s.Fire());

        Assert.Contains(201u, furni);
        Assert.Contains(1u, furni);
        Assert.Contains(100u, furni);
        Assert.Contains(inverted.Item.Id, furni);
        Assert.DoesNotContain(202u, furni);
        Assert.DoesNotContain(203u, furni);
    }

    [Fact]
    public void R105AFurniFilterLeavesTheUsersAlone()
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_users_bytype", Habbos());
        s.Selector("wf_slc_furni_area", S(filter: true));

        var context = s.Fire();

        Assert.Equal(new[] { 7, 8 }, s.Users(context));
        Assert.Empty(s.Furni(context));
    }

    // Library stacks without a trigger: L1 = S, L2 = S2, L3 = P, L4 = Habbos.
    [Theory]
    [InlineData("L1+L2", 0, new uint[] { 1, 202, 203 }, new int[0])]
    [InlineData("L1+L2", 1, new uint[] { 203 }, new int[0])]
    [InlineData("L1+L3", 1, new uint[] { 1 }, new int[0])]
    [InlineData("L1+L2+L3", 1, new uint[0], new int[0])]
    [InlineData("L1+L4", 0, new uint[] { 1, 202, 203 }, new[] { 7, 8 })]
    [InlineData("L1+L4", 1, new uint[0], new int[0])]
    [InlineData("L1+L6", 1, new uint[0], new int[0])]
    [InlineData("L6", 0, new uint[0], new int[0])]
    [InlineData("L7", 0, new uint[0], new int[0])]
    [InlineData("L9", 0, new uint[] { 203 }, new int[0])]
    public void R105RemoteCombinesItsPickedStacks(string picks, int type, uint[] furni, int[] users)
    {
        using var s = new SelectorStack();
        var stacks = new Dictionary<string, uint>
        {
            ["L1"] = s.Selector("wf_slc_furni_area", S(), 1, 0).Item.Id,
            ["L2"] = s.Selector("wf_slc_furni_area", S2(), 2, 0).Item.Id,
            ["L3"] = s.Selector("wf_slc_furni_picks", P(), 0, 1).Item.Id,
            ["L4"] = s.Selector("wf_slc_users_bytype", Habbos(), 5, 5).Item.Id,
            ["L6"] = s.Place(270, "wf_act_toggle_state", 6, 6).Id,
            ["L7"] = s.Selector("wf_slc_furni_area", S(filter: true), 7, 7).Item.Id
        };
        stacks["L9"] = s.Selector("wf_slc_furni_area", S(), 8, 8).Item.Id;
        s.Selector("wf_slc_furni_area", S2(filter: true), 8, 8);
        s.Selector("wf_slc_remote", Remote([.. picks.Split('+').Select(pick => stacks[pick])], type));

        var context = s.Fire();

        Assert.Equal(furni, s.Furni(context));
        Assert.Equal(users, s.Users(context));
    }

    [Theory]
    [InlineData(false, new uint[] { 1 })]
    [InlineData(true, new uint[] { 201 })]
    public void R105AFilteringRemoteKeepsOrRemovesItsStackFromThePool(bool invert, uint[] expected)
    {
        using var s = new SelectorStack();
        var library = s.Selector("wf_slc_furni_area", S(), 1, 0);
        s.Selector("wf_slc_furni_picks", P());
        s.Selector("wf_slc_remote", Remote([library.Item.Id], filter: true, invert: invert));

        Assert.Equal(expected, s.Furni(s.Fire()));
    }

    [Fact]
    public void R105ALoneFilteringRemoteSelectsNothing()
    {
        using var s = new SelectorStack();
        var library = s.Selector("wf_slc_furni_area", S(), 1, 0);
        s.Selector("wf_slc_remote", Remote([library.Item.Id], filter: true));

        var context = s.Fire();

        Assert.Empty(s.Furni(context));
        Assert.Empty(s.Users(context));
    }

    [Fact]
    public void R105ARemoteThatPicksItsOwnStackSelectsNothing()
    {
        using var s = new SelectorStack();
        var remote = s.Selector("wf_slc_remote", Remote([100]));

        Assert.Empty(s.Furni(s.Fire()));
        Assert.NotEqual(0u, remote.Item.Id);
    }
    // Not Has Hand Item is the opposite of the whole quantified Has Hand Item result, not a check that each user lacks the item. Both users are selected; only the first one holds item 2.
    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 1, false)]
    [InlineData(false, 0, true)]
    [InlineData(false, 1, true)]
    public void R105NotHasHandItemNegatesTheQuantifiedResult(bool holding, int quantifier, bool passes)
    {
        using var s = new SelectorStack();
        s.F.User.CarryItemId = holding ? 2 : 0;
        s.Selector("wf_slc_users_bytype", Habbos());
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_not_has_handitem", out var descriptor));
        var item = s.Place(290, "wf_cnd_not_has_handitem", 0, 0);
        var box = s.F.Room.GetWired().CreateConfiguredBox(item, descriptor)!;
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [2], UserSourceTypes = [200], Quantifier = quantifier };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime));
        box.ApplyConfiguration(runtime);
        Assert.True(s.F.Engine.Add(box));

        s.F.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = s.F.User });
        s.F.Engine.OnFastCycle();

        Assert.Empty(s.F.Errors);
        Assert.Equal(passes, s.Capture.Contexts.Count == 1);
    }
}
