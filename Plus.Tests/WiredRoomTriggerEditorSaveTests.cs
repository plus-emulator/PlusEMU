using Xunit;

namespace Plus.Tests;

// Raw saves of the room triggers through the real save packet.
public sealed class WiredRoomTriggerEditorSaveTests
{
    private const string VariableCount = "wiredfurni.error.invalid_variable_count";

    [Theory]
    [InlineData("wf_trg_enter_room")]
    [InlineData("wf_trg_leave_room")]
    public void ARoomTriggerRefusesAVariableWithTheCountKeyAndKeepsItsStoredForm(string name)
    {
        using var save = new WiredRawSave(name);
        Assert.True(save.Save(text: "abc").Accepted);
        var before = save.Stored();

        var result = save.Save(variables: ["user:5"]);

        Assert.Equal(("error", VariableCount), (result.Answer, result.Key));
        var after = save.Stored();
        Assert.Equal(before.Text, after.Text);
        Assert.Equal(before.Variables, after.Variables);
    }

    [Theory]
    [InlineData("wf_trg_enter_room")]
    [InlineData("wf_trg_leave_room")]
    public void ARoomTriggerStillAcceptsASaveWithNoVariable(string name)
    {
        using var save = new WiredRawSave(name);

        Assert.True(save.Save(ints: [1, 2], variables: []).Accepted);
        Assert.True(save.Save(text: "abc").Accepted);
    }
}
