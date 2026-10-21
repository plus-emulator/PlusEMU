using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The text add-on editors and the Says trigger text, saved as Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR105TextAddonEditorTests
{
    private const string Input = "wf_xtra_text_input_variable";
    private const string FurniName = "wf_xtra_text_output_furni_name";
    private const string Output = "wf_xtra_text_output_variable";

    private sealed record Result(bool Accepted, string? Key, int[] Ints, int[] Users, string Text, string[] Variables)
    {
        public override string ToString() => Accepted
            ? $"ok ints=[{string.Join(',', Ints)}] users=[{string.Join(',', Users)}] text='{Text}' vars=[{string.Join(',', Variables)}]"
            : $"refused key={Key ?? "generic"}";
    }

    // The steps of the save handler: fill the source slots, check the save, compile with the trigger-context sources, then let the box
    // validate and store.
    private static Result Save(string name, int[]? ints = null, string? text = null, string[]? variables = null, int[]? users = null)
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var item = world.AddItem(name, name.StartsWith("wf_trg", StringComparison.Ordinal) ? InteractionType.WiredTrigger : InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(name);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        native = native with
        {
            OwnedIntParams = ints is null ? native.OwnedIntParams : [.. ints],
            Text = text ?? native.Text,
            VariableIds = variables is null ? native.VariableIds : [.. variables],
            UserSourceTypes = users is null ? native.UserSourceTypes : [.. users]
        };

        var sources = WiredSourceRules.Check(descriptor, native, WiredNativeEditorProjection.Metadata(name));

        if (sources.Dropped) {
            return new(false, null, [], [], "", []);
        }

        if (sources.Key is { } sourceKey) {
            return new(false, sourceKey, [], [], "", []);
        }

        if (!WiredNativeEditorProjection.TryFillSourceSlots(descriptor, native, out native, out var countError)) {
            return new(false, countError, [], [], "", []);
        }

        if (WiredNativeEditorProjection.SaveRefusal(descriptor, native) is { } refusal) {
            return new(false, refusal, [], [], "", []);
        }

        if (!WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext)) {
            return new(false, WiredNativeEditorProjection.SourceRejectionKey(descriptor, native), [], [], "", []);
        }

        if (!WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out var error, id => world.Floor.ContainsKey(id))) {
            return new(false, error, [], [], "", []);
        }

        var stored = WiredNativeEditorProjection.HotelFrame(descriptor, box.Configuration.Origin!.Native);

        return new(true, null, [.. stored.OwnedIntParams], [.. stored.UserSourceTypes], stored.Text, [.. stored.VariableIds]);
    }

    private static Result Saved(string name, int[]? ints = null, string? text = null, string[]? variables = null, int[]? users = null)
    {
        var result = Save(name, ints, text, variables, users);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints ?? [])}] '{text}': {result}");

        return result;
    }

    private static void Refused(string key, string name, int[]? ints = null, string? text = null, string[]? variables = null, int[]? users = null)
    {
        var result = Save(name, ints, text, variables, users);
        Assert.False(result.Accepted, $"{name} '{text}': {result}");
        Assert.Equal("wiredfurni.error." + key, result.Key);
    }

    // Each box's own variable for the name probes: the capturer and Text Output Variable hold a context variable.
    private static string[]? Variables(string name) => name == FurniName ? null : ["ctx:10"];

    private static int[] SingleInts(string name) => name == Output ? [0, -20, 0] : [0];

    private static int[] MultipleInts(string name) => name == Output ? [1, -20, 0] : [1];

    [Theory]
    [InlineData(Input)]
    [InlineData(FurniName)]
    [InlineData(Output)]
    public void ANameIsStoredInLowerCaseAndHasOneToFortyLettersDigitsOrUnderscores(string name)
    {
        Result Single(string text) => Saved(name, SingleInts(name), text, Variables(name));

        Assert.Equal("amount", Single("amount").Text);
        Assert.Equal("amount", Single("Amount").Text);
        Assert.Equal("1", Single("1").Text);
        Assert.Equal("_", Single("_").Text);
        Assert.Equal(new string('a', 33), Single(new string('a', 33)).Text);
        Assert.Equal(new string('a', 40), Single(new string('a', 40)).Text);

        foreach (var text in new[] { "", new string('a', 41), new string('a', 100) }) {
            Refused("placeholder.name_length", name, SingleInts(name), text, Variables(name));
        }

        foreach (var text in new[] { "a b", "a-b", "a b-c", "ünï", " x ", "a.b", "$(a)", "#(a)" }) {
            Refused("placeholder.name_syntax", name, SingleInts(name), text, Variables(name));
        }
    }

    [Fact]
    public void TheCapturerNameRefusesATabAndMayBeTriggerText()
    {
        Refused("placeholder.name_syntax", Input, [0], "a\tb", ["ctx:10"]);
        Assert.Equal("trigger_text", Saved(Input, [0], "trigger_text", ["ctx:10"]).Text);
    }

    [Theory]
    [InlineData(FurniName)]
    [InlineData(Output)]
    public void APlaceholderNameIsNeverTriggerTextAndASingleNameEndsAtTheTab(string name)
    {
        Refused("placeholder.reserved", name, SingleInts(name), "trigger_text", Variables(name));
        Assert.Equal("a", Saved(name, SingleInts(name), "a\tb", Variables(name)).Text);
        Assert.Equal("tv", Saved(name, SingleInts(name), "tv\t,", Variables(name)).Text);
    }

    [Theory]
    [InlineData(FurniName)]
    [InlineData(Output)]
    public void TheMultipleModeDelimiterHasOneToFiveCharactersAndAThirdPartIsCut(string name)
    {
        Result Multiple(string text) => Saved(name, MultipleInts(name), text, Variables(name));

        Assert.Equal("tv\t,", Multiple("tv\t,").Text);
        Assert.Equal("tv\t,,,,,", Multiple("tv\t,,,,,").Text);
        Assert.Equal("tv\t ,", Multiple("tv\t ,").Text);
        Assert.Equal("tv\tabc", Multiple("tv\tabc").Text);
        Assert.Equal("tv\t, ", Multiple("tv\t, \t x").Text);
        Refused("placeholder.delimiter_empty", name, MultipleInts(name), "tv\t", Variables(name));
        Refused("placeholder.delimiter_empty", name, MultipleInts(name), "tv\t\t", Variables(name));
        Refused("placeholder.delimiter_length", name, MultipleInts(name), "tv\t,,,,,,", Variables(name));
        Refused("placeholder.name_length", name, MultipleInts(name), "\t,", Variables(name));
        Refused("placeholder.name_syntax", name, MultipleInts(name), "tv,", Variables(name));
    }

    [Theory]
    [InlineData(Input)]
    [InlineData(FurniName)]
    public void TheModeFlagStoresAnyNonZeroValueAsOne(string name)
    {
        var text = name == Input ? "amount" : "fn\t,";

        Assert.Equal([0], Saved(name, [0], text, Variables(name)).Ints);
        Assert.Equal([1], Saved(name, [1], text, Variables(name)).Ints);

        foreach (var flag in new[] { 2, -1, 100 }) {
            Assert.Equal([1], Saved(name, [flag], text, Variables(name)).Ints);
        }

        Assert.Equal([0], Saved(name, [], name == Input ? "amount" : "fn", Variables(name)).Ints);
    }

    [Fact]
    public void TextOutputVariableKeepsTheOfferedTargetsAndStoresTheFlagsAsOne()
    {
        Result Ints(params int[] ints) => Saved(Output, ints, ints is [not 0, ..] ? "tv\t," : "tv", ["user:11"]);

        foreach (var target in new[] { 0, 1, -10, -20 }) {
            Assert.Equal([0, target, 0], Ints(0, target, 0).Ints);
        }

        foreach (var target in new[] { 2, 3, 5, -1, -5, 100 }) {
            Assert.Equal([0, 0, 0], Ints(0, target, 0).Ints);
        }

        Assert.Equal([1, 1, 0], Ints(2, 1, 0).Ints);
        Assert.Equal([1, 1, 0], Ints(-1, 1, 0).Ints);
        Assert.Equal([0, 1, 1], Ints(0, 1, 2).Ints);
        Assert.Equal([0, 1, 1], Ints(0, 1, -1).Ints);
        Assert.Equal([0, 1, 0], Ints().Ints);
        Assert.Equal([0, 1, 0], Ints(0, 1, 0, 9).Ints);
    }

    [Fact]
    public void TextOutputVariableStoresAnyVariableWithAnyTarget()
    {
        Assert.Equal(["user:11"], Saved(Output, [0, 1, 0], "tv", ["user:11"]).Variables);
        Assert.Equal(["furni:12"], Saved(Output, [0, 0, 0], "tv", ["furni:12"]).Variables);
        Assert.Equal(["user:11"], Saved(Output, [0, 0, 0], "tv", ["user:11"]).Variables);
        Assert.Equal(["furni:12"], Saved(Output, [0, 1, 0], "tv", ["furni:12"]).Variables);
        Assert.Equal(["room:13"], Saved(Output, [1, -10, 0], "tv\t,", ["room:13"]).Variables);
        Assert.Equal(["ctx:10"], Saved(Output, [1, -20, 0], "tv\t,", ["ctx:10"]).Variables);
        Assert.Equal(["0"], Saved(Output, [0, 1, 0], "tv", ["0"]).Variables);
        Assert.Equal(["0"], Saved(Output, [0, 1, 0], "tv", [""]).Variables);
        Refused("invalid_variable_count", Output, [0, 1, 0], "tv", ["user:11", "ctx:10"]);
    }

    [Fact]
    public void TextOutputVariableTakesTheReachedAndClickedUserAndRefusesAnUnofferedOne()
    {
        foreach (var source in new[] { 0, 200, 201, 10, 11 }) {
            Assert.Equal([source], Saved(Output, [0, 1, 0], "tv", ["user:11"], [source]).Users);
        }

        Refused("invalid_user_source", Output, [0, 1, 0], "tv", ["user:11"], [100]);
    }

    [Fact]
    public void TheCapturerStoresAnyVariableOrNone()
    {
        foreach (var variable in new[] { "ctx:10", "user:11", "furni:12", "room:13" }) {
            Assert.Equal([variable], Saved(Input, [0], "amount", [variable]).Variables);
        }

        Assert.Equal(["0"], Saved(Input, [0], "amount", ["0"]).Variables);
        Assert.Equal(["0"], Saved(Input, [0], "amount", [""]).Variables);
        Refused("invalid_variable_count", Input, [0], "amount", ["ctx:10", "ctx:11"]);
    }

    [Fact]
    public void ASaysTextWithTheSameCaptureTwiceIsRefused()
    {
        Refused("capturer.duplicate", "wf_trg_says_something", [1, 0, 0], "k105e #(a) #(a)");
        Assert.Equal("k105c #(a) and #(b)", Saved("wf_trg_says_something", [1, 0, 0], "k105c #(a) and #(b)").Text);
        Assert.Equal("k105a #(a)", Saved("wf_trg_says_something", [1, 0, 0], "k105a #(a)").Text);
    }

    // Rows saved under the older, wider rules keep loading: names with spaces or capitals, an empty name, a long delimiter, a variable
    // of the target's own kind, and a Says text with the same capture twice.
    [Theory]
    [InlineData(Input, new[] { 0 }, "Amount", "ctx:10")]
    [InlineData(Input, new[] { 1 }, "count_2", "ctx:10")]
    [InlineData(FurniName, new[] { 0 }, "", null)]
    [InlineData(FurniName, new[] { 1 }, "Old Name\t;;;;;;;;", null)]
    [InlineData(FurniName, new[] { 0 }, "$(fn)\t, ", null)]
    [InlineData(Output, new[] { 1, 1, 0 }, "Old Name\t | | | | ", "user:11")]
    [InlineData(Output, new[] { 0, 0, 1 }, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "furni:12")]
    [InlineData("wf_trg_says_something", new[] { 1, 0, 0 }, "k #(a) #(a)", null)]
    public void AStoredRowOfTheOlderRulesStillLoads(string name, int[] ints, string text, string? variable)
    {
        using var world = new G1World();
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var item = world.AddItem(name, name.StartsWith("wf_trg", StringComparison.Ordinal) ? InteractionType.WiredTrigger : InteractionType.WiredEffect);
        var box = world.Wired.CreateConfiguredBox(item) ?? throw new InvalidOperationException(name);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [.. ints],
            Text = text
        };
        native = variable is null ? native : native with { VariableIds = [variable] };

        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var runtime, storedRow: true), name);
        Assert.True(box.TryValidateConfiguration(runtime, out var validated, out var error), error);
        box.ApplyConfiguration(validated);
    }
}
