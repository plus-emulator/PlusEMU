using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// The text add-ons in a fired stack, and the numbers a capturer reads.
public partial class ModernWiredRuntimeTests
{
    // A text add-on saved through the editor, on the stack's tile.
    private static void TextAddon(SelectorStack s, uint id, string name, int[] ints, string text, string[] variables, int[]? furni = null,
        int[]? users = null, uint[]? picks = null)
    {
        var item = s.Place(id, name, 0, 0);
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var box = s.F.Room.GetWired().CreateConfiguredBox(item, descriptor)!;
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = native with
        {
            OwnedIntParams = [.. ints],
            Text = text,
            VariableIds = [.. variables],
            FurniSourceTypes = furni is null ? native.FurniSourceTypes : [.. furni],
            UserSourceTypes = users is null ? native.UserSourceTypes : [.. users],
            PrimaryItems = [.. (picks ?? []).Select(pick => new WiredNativeItemReference(pick, false))]
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext), name);
        Assert.True(box.TryValidateConfiguration(runtime, out var validated, out var error), error);
        box.ApplyConfiguration(validated);
        Assert.True(s.F.Engine.Add(box));
    }

    private static string Shown(SelectorStack s, string message)
    {
        var context = s.Fire();

        return context.Policy.FormatText(context, message);
    }

    [Fact]
    public void TextOutputVariablePrintsNullForAFurniWithoutTheVariableInBothModes()
    {
        using var s = new SelectorStack();
        s.Give(s.Blocks[0], 3);
        s.Give(s.Blocks[1], 5);
        TextAddon(s, 400, "wf_xtra_text_output_variable", [1, 0, 0], "tv\t,", ["furni:10"], furni: [100],
            picks: [s.Blocks[0].Id, s.Blocks[1].Id, s.Blocks[2].Id]);
        TextAddon(s, 401, "wf_xtra_text_output_variable", [0, 0, 0], "one", ["furni:10"], furni: [100], picks: [s.Blocks[2].Id]);

        Assert.Equal("v2 3,5,null / [null]", Shown(s, "v2 $(tv) / [$(one)]"));
    }

    [Fact]
    public void TextOutputVariableLeavesThePlaceholderWhenNoFurniIsPicked()
    {
        using var s = new SelectorStack();
        s.Give(s.Blocks[0], 3);
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 0, 0], "tv", ["furni:10"], furni: [100]);

        Assert.Equal("v7 [$(tv)]", Shown(s, "v7 [$(tv)]"));
    }

    [Fact]
    public void TextOutputVariablePrintsABotLikeAnyOtherUser()
    {
        using var s = new SelectorStack();
        s.Give(s.F.User, 7);
        s.Give(s.Bob, 8);
        Bot(s.F.Room, 20);
        s.Selector("wf_slc_users_area", UsersArea(0, 0, 9, 3));
        TextAddon(s, 400, "wf_xtra_text_output_variable", [1, 1, 0], "tv\t/", ["user:11"], users: [200]);

        Assert.Equal(["7", "8", "null"], Shown(s, "$(tv)").Split('/').Order());
    }

    [Fact]
    public void TextOutputVariablePrintsNullForASelectedBotWithoutTheVariable()
    {
        using var s = new SelectorStack();
        SelectedBot(s);
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 1, 0], "tv", ["user:11"], users: [200]);
        TextAddon(s, 401, "wf_xtra_text_output_variable", [1, 1, 0], "all\t,", ["user:11"], users: [200]);

        Assert.Equal("v5 [null] [null]", Shown(s, "v5 [$(tv)] [$(all)]"));
    }

    [Fact]
    public void TextOutputVariablePrintsTheValueOfASelectedBot()
    {
        using var s = new SelectorStack();
        s.Give(SelectedBot(s), 8);
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 1, 0], "tv", ["user:11"], users: [200]);
        TextAddon(s, 401, "wf_xtra_text_output_variable", [1, 1, 0], "all\t,", ["user:11"], users: [200]);

        Assert.Equal("v5 [8] [8]", Shown(s, "v5 [$(tv)] [$(all)]"));
    }

    private static RoomUser SelectedBot(SelectorStack s)
    {
        var bot = Bot(s.F.Room, 20);
        bot.BotData.Name = "Robbie";
        s.Selector("wf_slc_users_byname", UsersByName("Robbie"));

        return bot;
    }

    // The triggering user without the variable shows "null".
    [Fact]
    public void TextOutputVariablePrintsNullForTheTriggeringUserWithoutTheVariable()
    {
        using var s = new SelectorStack();
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 1, 0], "tv", ["user:11"], users: [0]);

        Assert.Equal("a=null", Shown(s, "a=$(tv)"));
    }

    [Fact]
    public void TextOutputVariableWithoutAVariableLeavesThePlaceholder()
    {
        using var s = new SelectorStack();
        s.Give(s.F.User, 7);
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 1, 0], "tv", ["0"]);

        Assert.Equal("v11 [$(tv)]", Shown(s, "v11 [$(tv)]"));
    }

    [Fact]
    public void TextOutputFurniNameLeavesThePlaceholderWhenNoFurniIsPicked()
    {
        using var s = new SelectorStack();
        TextAddon(s, 400, "wf_xtra_text_output_furni_name", [0], "fn", [], furni: [100]);
        TextAddon(s, 401, "wf_xtra_text_output_furni_name", [1], "all\t,", [], furni: [100]);

        Assert.Equal("v13 [$(fn)] [$(all)]", Shown(s, "v13 [$(fn)] [$(all)]"));
    }

    [Fact]
    public void TextOutputFurniNameWithShowMultipleFillsAnEmptySelectionWithNothing()
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_furni_with_var", Points(false, false));
        TextAddon(s, 400, "wf_xtra_text_output_furni_name", [1], "fn\t,", [], furni: [200]);

        Assert.Equal("src []", Shown(s, "src [$(fn)]"));
    }

    [Fact]
    public void TextOutputFurniNameWithShowMultiplePrintsTheSelectedFurni()
    {
        using var s = new SelectorStack();
        s.Blocks[2].Definition.FurniName = "Block";
        s.Give(s.Blocks[2], 2);
        s.Selector("wf_slc_furni_with_var", Points(false, false));
        TextAddon(s, 400, "wf_xtra_text_output_furni_name", [1], "fn\t,", [], furni: [200]);

        Assert.Equal("src [Block]", Shown(s, "src [$(fn)]"));
    }

    // Furni Name prints the name of the furnidata ("Colour Tile"), not the shop name.
    [Fact]
    public void TextOutputFurniNamePrintsTheFurnidataName()
    {
        using var s = new SelectorStack();
        s.Blocks[0].Definition.PublicName = "Block 1";
        s.Blocks[0].Definition.FurniName = "Block";
        TextAddon(s, 400, "wf_xtra_text_output_furni_name", [0], "fn", [], furni: [100], picks: [s.Blocks[0].Id]);

        Assert.Equal("Block", Shown(s, "$(fn)"));
    }

    [Fact]
    public void ATextOutputNameOfFortyCharactersIsReplaced()
    {
        var name = new string('a', 40);
        using var s = new SelectorStack();
        s.Give(s.Blocks[0], 3);
        TextAddon(s, 400, "wf_xtra_text_output_furni_name", [0], name, [], furni: [100], picks: [s.Blocks[0].Id]);
        TextAddon(s, 401, "wf_xtra_text_output_variable", [0, 0, 0], "b" + name[1..], ["furni:10"], furni: [100], picks: [s.Blocks[0].Id]);

        Assert.Equal($"{s.Blocks[0].Definition.PublicName} 3", Shown(s, $"$({name}) $(b{name[1..]})"));
    }

    [Theory]
    [InlineData("k105a 5", 5L)]
    [InlineData("k105a -3", -3L)]
    [InlineData("k105a 0", 0L)]
    [InlineData("k105a 007", 7L)]
    [InlineData("k105a +4", 4L)]
    [InlineData("k105a -0", 0L)]
    [InlineData("k105a 2147483647", 2147483647L)]
    [InlineData("k105a -2147483648", -2147483648L)]
    [InlineData("k105a 2147483648", 2147483648L)]
    [InlineData("K105A 9", 9L)]
    [InlineData("k105a 1.5", null)]
    [InlineData("k105a abc", null)]
    [InlineData("k105a 0x10", null)]
    [InlineData("k105a 5 ", null)]
    [InlineData("k105a  5", null)]
    public void AnExactCaptureReadsAWholeNumberWithoutSpaces(string said, long? expected)
    {
        var rig = G3CapturerStack("k105a #(a)", 1, ("a", "ctx:10", 0));
        using var f = rig.F;
        G3Say(rig, said);

        if (expected is { } value) {
            Assert.Equal(value, Assert.Single(rig.Recorder.Runs)[0]);
        }
        else {
            Assert.Empty(rig.Recorder.Runs);
        }

        Assert.Empty(f.Errors);
    }
}
