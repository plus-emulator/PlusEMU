using Xunit;

namespace Plus.Tests;

// Filter Users saved through the real packet path.
public sealed class WiredFilterUsersEditorTests
{
    private const string Name = "wf_xtra_filter_users";

    [Fact]
    public void ATextIsDropped()
    {
        using var save = new WiredRawSave(Name);
        var result = save.Save(ints: [7, 0, 1], text: "abc");

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([7, 0, 1], result.Ints);
        Assert.Equal("", result.Text);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(10)]
    [InlineData(11)]
    public void TheUserSourceIsKeptForTheSourcesTheBoxTakes(int source)
    {
        using var save = new WiredRawSave(Name);
        var result = save.Save(users: [source]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([source], result.Users);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void AUserSourceTheBoxDoesNotTakeIsRefused(int source)
    {
        using var save = new WiredRawSave(Name);

        Assert.Equal("wiredfurni.error.invalid_user_source", save.Save(users: [source]).Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(201)]
    public void TheFurniSourceIsKeptForTheSourcesTheBoxTakes(int source)
    {
        using var save = new WiredRawSave(Name);
        var result = save.Save(furni: [source]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([source], result.Furni);
    }

    [Fact]
    public void ASecondVariableIsRefusedWithItsCount()
    {
        using var save = new WiredRawSave(Name);

        Assert.Equal("wiredfurni.error.invalid_variable_count", save.Save(variables: ["1", "2"]).Key);
    }
}
