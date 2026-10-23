using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredNativeLifecycleTests
{
    // An echo that picks another echo is refused with wiredfurni.error.echo.self_reference; an echo of a plain user variable
    // saves.
    [Fact]
    public void EchoOfAnEchoIsRefusedWithTheSelfReferenceKey()
    {
        var f = new World();
        f.Room.OwnerId = 5;
        var variable = f.Item(4);
        variable.Definition.InteractionName = "wf_var_user";
        var first = f.Item(6);
        first.Definition.InteractionName = "wf_var_echo";
        var firstBox = (WiredVariableDefinitionBox)f.Wired.CreateConfiguredBox(first)!;
        var echoOfVariable = WiredNativeEditorProjection.DefaultNative(firstBox.Descriptor) with { Text = "r80vechoa", VariableIds = ["user:4"] };
        Assert.True(WiredNativeEditorProjection.TryCompile(first.Id, firstBox.Descriptor, echoOfVariable, out var runtime));
        Assert.True(firstBox.TryValidateConfiguration(runtime, out _, out var error), error);
        WiredNativeTestSupport.Install(firstBox, echoOfVariable);
        Assert.True(f.Wired.AddBox(firstBox));

        var second = f.Item(7);
        second.Definition.InteractionName = "wf_var_echo";
        var secondBox = (WiredVariableDefinitionBox)f.Wired.CreateConfiguredBox(second)!;
        var echoOfEcho = WiredNativeEditorProjection.DefaultNative(secondBox.Descriptor) with { Text = "r80vechob", VariableIds = ["user:6"] };
        Assert.True(WiredNativeEditorProjection.TryCompile(second.Id, secondBox.Descriptor, echoOfEcho, out runtime));
        Assert.False(secondBox.TryValidateConfiguration(runtime, out _, out error));
        Assert.Equal("wiredfurni.error.echo.self_reference", error);
    }
}
