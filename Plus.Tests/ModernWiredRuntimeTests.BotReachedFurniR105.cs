using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Bot Reaches Furni stacks saved through the native editor row and fired through the engine.
public partial class ModernWiredRuntimeTests
{
    private static string BotReachedRow(string name, int furniSource = 100, int userSource = 100, string picks = "[]") =>
        $$$"""{"Version":2,"Category":0,"NativeCode":13,"OwnedIntParams":[],"Text":"{{{name}}}","PrimaryItems":{{{picks}}},"SecondaryItems":[],"FurniSourceTypes":[{{{furniSource}}}],"UserSourceTypes":[{{{userSource}}}],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}""";

    // Users By Type: Habbo 1, Bot 4.
    private static string UsersOfType(int type) => Row(2, $"[{type}]", false, false);

    private static RoomUser Robbie(SelectorStack s) => AddNamedBot(s.F, 9, "Robbie", 2, 2);

    private static bool Reaches(SelectorStack s, RoomUser bot, uint itemId)
    {
        s.Capture.Contexts.Clear();
        s.F.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedFurni) { Actor = bot, EventItem = s.F.Items[itemId] });
        s.F.Engine.OnFastCycle();
        Assert.Empty(s.F.Errors);

        return s.Capture.Contexts.Count == 1;
    }

    [Theory]
    [InlineData("Robbie", true)]
    [InlineData("ROBBIE", true)]
    [InlineData("robbie", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("Robbie ", false)]
    [InlineData(" Robbie", false)]
    [InlineData("Rob", false)]
    [InlineData("Robbie2", false)]
    [InlineData("R*", false)]
    [InlineData("Other", false)]
    public void BotReachesFurniMatchesTheExactNameIgnoringCase(string name, bool fires)
    {
        using var s = new SelectorStack();
        var bot = Robbie(s);
        s.Selector("wf_trg_bot_reached_stf", BotReachedRow(name, picks: Pick(1)));

        Assert.Equal(fires, Reaches(s, bot, 1));
    }

    [Theory]
    [InlineData("", 100, new int[0], false)]
    [InlineData("Other", 100, new int[0], false)]
    [InlineData("Robbie", 100, new int[0], true)]
    [InlineData("", 200, new[] { 4 }, true)]
    [InlineData("Other", 200, new[] { 4 }, true)]
    [InlineData("Robbie", 200, new[] { 1 }, false)]
    [InlineData("Robbie", 200, new int[0], false)]
    [InlineData("", 200, new int[0], false)]
    [InlineData("", 200, new[] { 1 }, false)]
    [InlineData("", 200, new[] { 1, 4 }, true)]
    public void BotReachesFurniUsesTheNameForSource100AndOnlyTheSelectionForSource200(string name, int userSource, int[] types, bool fires)
    {
        using var s = new SelectorStack();
        var bot = Robbie(s);
        s.Selector("wf_trg_bot_reached_stf", BotReachedRow(name, userSource: userSource, picks: Pick(1)));

        foreach (var type in types) {
            s.Selector("wf_slc_users_bytype", UsersOfType(type));
        }

        Assert.Equal(fires, Reaches(s, bot, 1));
    }

    [Fact]
    public void BotReachesFurniWithoutAPickNeverFires()
    {
        using var s = new SelectorStack();
        var bot = Robbie(s);
        s.Selector("wf_trg_bot_reached_stf", BotReachedRow("Robbie"));

        Assert.False(Reaches(s, bot, 1));
        Assert.False(Reaches(s, bot, 201));
    }

    [Fact]
    public void BotReachesFurniWithTwoPicksFiresForEither()
    {
        using var s = new SelectorStack();
        var bot = Robbie(s);
        s.Selector("wf_trg_bot_reached_stf", BotReachedRow("Robbie", picks: "[" + string.Join(",", Pick(1)[1..^1], Pick(201)[1..^1]) + "]"));

        Assert.True(Reaches(s, bot, 1));
        Assert.True(Reaches(s, bot, 201));
        Assert.False(Reaches(s, bot, 202));
    }

    [Theory]
    [InlineData("Robbie", true)]
    [InlineData("", false)]
    public void BotReachesFurniWithAFurniSelectorFiresForTheSelectedFurni(string name, bool fires)
    {
        using var s = new SelectorStack();
        var bot = Robbie(s);
        s.Selector("wf_trg_bot_reached_stf", BotReachedRow(name, furniSource: 200));
        s.Selector("wf_slc_furni_picks", Row(1, "[]", false, false, picks: Pick(1)));

        Assert.Equal(fires, Reaches(s, bot, 1));
        Assert.False(Reaches(s, bot, 201));
    }
}
