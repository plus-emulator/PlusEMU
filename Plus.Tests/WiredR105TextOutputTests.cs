using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Text Output Variable and Text Output Furni Name saves as Habbo stores them.
public sealed class WiredR105TextOutputTests
{
    private const string Variable = "wf_xtra_text_output_variable";
    private const string FurniName = "wf_xtra_text_output_furni_name";
    private const string Prefix = "wiredfurni.error.placeholder.";

    // Text Output Furni Name has the show multiple flag only.
    private static int[] Fit(string name, int[] ints) => name == FurniName && ints.Length > 1 ? ints[..1] : ints;

    private static Result Saved(string name, int[] ints, string text = "tv", string[]? variables = null, int[]? users = null)
    {
        var result = Save(name, Fit(name, ints), users: users, text: text, variables: name == Variable ? variables ?? ["0"] : null);
        Assert.True(result.Accepted, $"{name} [{string.Join(',', ints)}] '{text}': {Show(result)}");

        return result;
    }

    private static string? Key(string name, int[] ints, string text, string[]? variables = null, int[]? users = null) =>
        Save(name, Fit(name, ints), users: users, text: text, variables: name == Variable ? variables ?? ["0"] : null) is { Accepted: false } result ? result.Key : "accepted";

    [Theory]
    [InlineData(2, 1)]
    [InlineData(-1, 1)]
    [InlineData(100, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void ShowMultipleStoresAnyNonZeroValueAsOne(int sent, int stored) =>
        Assert.Equal([stored, 1, 0], Saved(Variable, [sent, 1, 0], "tv\t,").Ints);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    public void TextModeStoresAnyNonZeroValueAsOne(int sent, int stored) =>
        Assert.Equal([0, 1, stored], Saved(Variable, [0, 1, sent]).Ints);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(-10, -10)]
    [InlineData(-20, -20)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(5, 0)]
    [InlineData(-1, 0)]
    [InlineData(-5, 0)]
    [InlineData(100, 0)]
    public void ATargetTheEditorDoesNotOfferIsStoredAsZero(int sent, int stored) =>
        Assert.Equal([0, stored, 0], Saved(Variable, [0, sent, 0]).Ints);

    [Fact]
    public void MissingIntsTakeTheFrameDefaultAndExtraIntsAreDropped()
    {
        Assert.Equal([0, 1, 0], Saved(Variable, []).Ints);
        Assert.Equal([0, 1, 0], Saved(Variable, [0, 1, 0, 9]).Ints);
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(-1, 1)]
    [InlineData(100, 1)]
    [InlineData(0, 0)]
    public void FurniNameStoresAnyNonZeroShowMultipleAsOne(int sent, int stored) => Assert.Equal([stored], Saved(FurniName, [sent], "tv\t,").Ints);

    [Fact]
    public void FurniNameWithoutIntsIsAccepted() => Assert.Equal([0], Saved(FurniName, []).Ints);

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void ANameIsStoredInLowerCaseAndASingleNameDropsTheDelimiter(string name)
    {
        Assert.Equal("amount", Saved(name, [0, 1, 0], "Amount").Text);
        Assert.Equal("a", Saved(name, [0, 1, 0], "a\tb").Text);
        Assert.Equal("tv", Saved(name, [0, 1, 0], "tv\t,").Text);
    }

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void ANameOfOneToFortyCharactersIsAccepted(string name)
    {
        Assert.Equal("1", Saved(name, [0, 1, 0], "1").Text);
        Assert.Equal("_", Saved(name, [0, 1, 0], "_").Text);
        Assert.Equal(new string('a', 33), Saved(name, [0, 1, 0], new string('a', 33)).Text);
        Assert.Equal(new string('a', 40), Saved(name, [0, 1, 0], new string('a', 40)).Text);
    }

    // A box left on its frame (no name, no variable) is not a save yet, so these cases name a variable or ask for several values.
    [Theory]
    [InlineData(Variable, "")]
    [InlineData(Variable, "\t,")]
    [InlineData(FurniName, "\t,")]
    public void AnEmptyNameIsAnsweredWithTheNameLengthKey(string name, string text) =>
        Assert.Equal(Prefix + "name_length", Key(name, [1, 1, 0], text, ["user:5"]));

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void ATooLongNameIsAnsweredWithTheNameLengthKey(string name)
    {
        Assert.Equal(Prefix + "name_length", Key(name, [0, 1, 0], new string('a', 41)));
        Assert.Equal(Prefix + "name_length", Key(name, [0, 1, 0], new string('a', 100)));
    }

    [Theory]
    [InlineData(Variable, "a b")]
    [InlineData(Variable, "a-b")]
    [InlineData(Variable, "a b-c")]
    [InlineData(Variable, "ünï")]
    [InlineData(Variable, " x ")]
    [InlineData(Variable, "a.b")]
    [InlineData(Variable, "$(a)")]
    [InlineData(Variable, "#(a)")]
    [InlineData(FurniName, "a b")]
    [InlineData(FurniName, "a.b")]
    public void ANameWithOtherCharactersIsAnsweredWithTheNameSyntaxKey(string name, string text) =>
        Assert.Equal(Prefix + "name_syntax", Key(name, [0, 1, 0], text));

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void TheNameTriggerTextIsReserved(string name) => Assert.Equal(Prefix + "reserved", Key(name, [0, 1, 0], "trigger_text"));

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void AMultipleDelimiterOfOneToFiveCharactersIsKept(string name)
    {
        Assert.Equal("tv\t,", Saved(name, [1, 1, 0], "tv\t,").Text);
        Assert.Equal("tv\t,,,,,", Saved(name, [1, 1, 0], "tv\t,,,,,").Text);
        Assert.Equal("tv\t,x", Saved(name, [1, 1, 0], "tv\t,x\ty").Text);
    }

    [Theory]
    [InlineData(Variable, "tv\t")]
    [InlineData(Variable, "tv\t\t")]
    [InlineData(FurniName, "tv\t")]
    public void AnEmptyMultipleDelimiterIsAnsweredWithTheDelimiterEmptyKey(string name, string text) =>
        Assert.Equal(Prefix + "delimiter_empty", Key(name, [1, 1, 0], text));

    [Theory]
    [InlineData(Variable)]
    [InlineData(FurniName)]
    public void ATooLongMultipleDelimiterIsAnsweredWithTheDelimiterLengthKey(string name) =>
        Assert.Equal(Prefix + "delimiter_length", Key(name, [1, 1, 0], "tv\t,,,,,,"));

    [Fact]
    public void ATargetThatDoesNotFitTheVariableIsAccepted()
    {
        Assert.Equal(["furni:5"], Saved(Variable, [0, 0, 0], variables: ["furni:5"]).Variables);
        Assert.Equal(["user:5"], Saved(Variable, [0, 0, 0], variables: ["user:5"]).Variables);
        Assert.Equal(["furni:5"], Saved(Variable, [0, 1, 0], variables: ["furni:5"]).Variables);
    }

    [Fact]
    public void NoVariableIsStoredAsTheEmptySlot() => Assert.Equal(["0"], Saved(Variable, [0, 1, 0], variables: []).Variables);

    [Fact]
    public void TwoVariablesAreAnsweredWithTheVariableCountKey() =>
        Assert.Equal("wiredfurni.error.invalid_variable_count", Key(Variable, [0, 1, 0], "tv", ["user:5", "furni:6"]));

    [Fact]
    public void AReachedUserSourceIsAcceptedAndAPickedUserSourceIsRefused()
    {
        Assert.Equal([10], Saved(Variable, [0, 1, 0], users: [10]).Users);
        Assert.Equal("wiredfurni.error.invalid_user_source", Key(Variable, [0, 1, 0], "tv", users: [100]));
    }
}
