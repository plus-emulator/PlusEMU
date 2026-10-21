using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;
using static Plus.Tests.WiredR102EditorSave;

namespace Plus.Tests;

// Init Transaction has three variable slots. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR105InitTransactionTests
{
    private const string Name = "wf_act_init_transaction";

    [Fact]
    public void TheEditorFrameHasThreeEmptyVariableSlots()
    {
        Assert.True(WiredBoxRegistry.TryGet(Name, out var descriptor));

        Assert.Equal(["0", "0", "0"], WiredNativeEditorProjection.DefaultNative(descriptor).VariableIds.ToArray());
    }

    [Fact]
    public void ASaveKeepsThreeVariableSlots()
    {
        var saved = Save(Name, [0, 10, 0, -20, 0, 300], variables: ["0", "0", "0"]);

        Assert.True(saved.Accepted, Show(saved));
        Assert.Equal(["0", "0", "0"], saved.Variables);
    }

    [Fact]
    public void ASaveWithOneVariableFillsTheOtherSlots()
    {
        var saved = Save(Name, [0, 10, 0, -20, 0, 300], variables: ["0"]);

        Assert.True(saved.Accepted, Show(saved));
        Assert.Equal(["0", "0", "0"], saved.Variables);
    }

    [Fact]
    public void ARowStoredWithOneVariableKeepsLoading()
    {
        Assert.True(WiredBoxRegistry.TryGet(Name, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { VariableIds = ["0"] };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, native, out _, storedRow: true));
    }
}
