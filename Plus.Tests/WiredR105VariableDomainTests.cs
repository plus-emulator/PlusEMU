using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// The box accepts a variable of another domain than its target, the way Habbo does. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR105VariableDomainTests
{
    [Theory]
    [InlineData("wf_xtra_filter_furni_by_var", new[] { 5, 0, 0, 1 }, "user:5", "0")]
    [InlineData("wf_xtra_filter_users_by_var", new[] { 5, 0, 0, 1 }, "furni:5", "0")]
    [InlineData("wf_xtra_filter_furni_by_var", new[] { 5, 0, 1, 0 }, "furni:5", "furni:7")]
    [InlineData("wf_xtra_filter_users_by_var", new[] { 5, 0, 1, 1 }, "user:5", "furni:7")]
    public void AFilterAcceptsAVariableOrOperandOfAnotherDomain(string name, int[] ints, string variable, string operand)
    {
        using var world = new G1World();

        Assert.True(Save(world, name, ints, variables: [variable, operand]).Accepted);
    }

    [Theory]
    [InlineData(new[] { 0, 0, 0 }, "furni:5")]
    [InlineData(new[] { 0, 1, 0 }, "furni:5")]
    [InlineData(new[] { 0, 0, 0 }, "user:5")]
    public void TextOutputVariableAcceptsAVariableThatDoesNotFitItsTarget(int[] ints, string variable)
    {
        using var world = new G1World();

        Assert.True(Save(world, "wf_xtra_text_output_variable", ints, text: "tv", variables: [variable]).Accepted);
    }
}
