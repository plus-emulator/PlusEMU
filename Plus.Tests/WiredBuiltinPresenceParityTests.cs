using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class WiredBuiltinPresenceParityTests
{
    [Fact]
    public void AreaFlagCreateRemoveReachBuiltinWithoutTreatingAbsenceAsNumericZero()
    {
        var holder = new WiredVariableHolder(WiredVariableTarget.Furni, 1, 1);
        var frame = new WiredVariableFrame(1, [holder]) { Trigger = [holder] };
        var flags = new Presence();
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System, flags);
        var executor = new WiredVariableExecutors(module, TimeProvider.System);
        var give = new WiredConfiguration { IntParams = [1, 0, 999, 0, 0], Text = "internal:~area_hide.inverted" };
        Assert.True(executor.Execute("wf_act_give_var", WiredNativeTestSupport.Scalar("wf_act_give_var", give), frame));
        Assert.NotNull(module.Read(new(holder.Target, give.Text), holder, frame));
        Assert.False(executor.Execute("wf_act_give_var", WiredNativeTestSupport.Scalar("wf_act_give_var", give), frame));
        Assert.False(executor.Execute("wf_act_change_var_val", WiredNativeTestSupport.Scalar("wf_act_change_var_val", new() { IntParams = [1, 0, 0, 0, 1, 0, 0, 0, 0], Text = give.Text }), frame));
        Assert.True(executor.Execute("wf_act_remove_var", WiredNativeTestSupport.Scalar("wf_act_remove_var", new() { IntParams = [1, 0, 0], Text = give.Text }), frame));
        Assert.Null(module.Read(new(holder.Target, give.Text), holder, frame));
        Assert.False(executor.Execute("wf_act_remove_var", WiredNativeTestSupport.Scalar("wf_act_remove_var", new() { IntParams = [1, 0, 0], Text = give.Text }), frame));
        Assert.Empty(module.DrainChanges()); // Native metadata prohibits interception for these presence flags.
        Assert.False(WiredVariableExecutors.TryValidate("wf_act_give_var", WiredNativeTestSupport.Scalar("wf_act_give_var", give with { Text = "internal:@is_stackable" }), out _));
        Assert.False(module.Mutate(new(holder.Target, give.Text), holder, WiredVariableMutation.Give, 0, new(1, [])));
        Assert.False(module.Mutate(new(holder.Target, give.Text), holder, WiredVariableMutation.Give, 0, new(2, [holder])));
    }

    [Fact]
    public void GiveVariableSavesWithAGlobalAndGivesNothing()
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703): the save is accepted and "= 0" still passes after giving 7.
        var give = WiredNativeTestSupport.Scalar("wf_act_give_var", new() { IntParams = [(int)WiredVariableTarget.Global, 1, 7, 0, 0], Text = "custom:42" });
        Assert.True(WiredVariableExecutors.TryValidate("wf_act_give_var", give, out _));
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System, new Presence());
        Assert.False(new WiredVariableExecutors(module, TimeProvider.System).Execute("wf_act_give_var", give, new(1, [])));
        Assert.False(WiredVariableExecutors.TryValidate("wf_act_remove_var",
            WiredNativeTestSupport.Scalar("wf_act_remove_var", new() { IntParams = [(int)WiredVariableTarget.Global, 0, 0], Text = "custom:42" }), out _));
    }

    private sealed class Presence : IWiredBuiltinVariables
    {
        private bool _present;
        public bool HasValue(WiredVariableReference reference) => false;
        public WiredVariableValue? Read(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableFrame frame) => _present ? new(1, null, null) : null;
        public bool Write(WiredVariableReference reference, WiredVariableHolder holder, int value, WiredVariableFrame frame) => false;
        public bool MutatePresence(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation, WiredVariableFrame frame)
        {
            var desired = mutation != WiredVariableMutation.Remove;

            if (desired == _present) {
                return false;
            }

            _present = desired;

            return true;
        }
    }
    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => null;
        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
