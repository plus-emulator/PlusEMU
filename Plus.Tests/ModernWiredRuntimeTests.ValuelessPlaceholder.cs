using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void TextOutputVariablePrintsNullForAHeldVariableWithoutAValue()
    {
        using var s = new SelectorStack();
        var holder = WiredVariableRuntimeFrames.FurniHolder(s.Blocks[0]);
        Assert.True(s.Module.Mutate(new(WiredVariableTarget.Furni, "custom:12"), holder, WiredVariableMutation.Give, 0, new(1, [holder])));
        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 0, 0], "one", ["furni:12"], furni: [100], picks: [s.Blocks[0].Id]);
        TextAddon(s, 401, "wf_xtra_text_output_variable", [0, 0, 1], "two", ["furni:12"], furni: [100], picks: [s.Blocks[0].Id]);

        Assert.Equal("[null] [null]", Shown(s, "[$(one)] [$(two)]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TextOutputVariablePrintsNullForCanMoveFreelyHeldOrNot(bool held)
    {
        using var s = new SelectorStack();
        var holder = WiredVariableRuntimeFrames.FurniHolder(s.Blocks[0]);

        if (held) {
            Assert.True(s.Module.Mutate(new(WiredVariableTarget.Furni, "internal:@can_move_freely"), holder, WiredVariableMutation.Give, 1, new(1, [holder])));
        }

        TextAddon(s, 400, "wf_xtra_text_output_variable", [0, 0, 0], "one", ["-2000"], furni: [100], picks: [s.Blocks[0].Id]);

        Assert.Equal("[null]", Shown(s, "[$(one)]"));
    }
}
