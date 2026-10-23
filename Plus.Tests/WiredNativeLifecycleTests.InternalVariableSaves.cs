using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// Give Variable (initial 32, override), Change Variable Value (assign 33) and Remove Variable all save on the internal user
// variable @habbicon ("-245"), and none of them changes it: a placeholder reads null after each.
public sealed partial class WiredNativeLifecycleTests
{
    private const string HabbiconId = "-245";

    [Fact]
    public void GiveChangeAndRemoveVariableSaveOnHabbicon()
    {
        var user = (int)WiredVariableTarget.User;
        var give = new WiredConfiguration { IntParams = [user, 1, 32, 0, 0], VariableIds = [HabbiconId] };
        var change = new WiredConfiguration { IntParams = [user, 0, 0, 33, user, 0, 0, 0, 0], VariableIds = [HabbiconId, "n"] };
        var remove = new WiredConfiguration { IntParams = [user, 0, 0], VariableIds = [HabbiconId] };

        Assert.True(WiredVariableExecutors.TryValidate("wf_act_give_var", give, out var giveError), giveError);
        Assert.True(WiredVariableExecutors.TryValidate("wf_act_change_var_val", change, out var changeError), changeError);
        Assert.True(WiredVariableExecutors.TryValidate("wf_act_remove_var", remove, out var removeError), removeError);
    }

    [Fact]
    public void HabbiconIsNeverGivenChangedOrRemoved()
    {
        var f = new World();
        var actor = f.Bot();
        f.PrepareVariables();
        var frame = WiredVariableRuntimeFrames.Create(f.Context());
        Assert.True(WiredVariableDescription.TryParseCatalogId(HabbiconId, out var target, out var token));
        var habbicon = new WiredVariableReference(target, token);
        var holder = WiredVariableRuntimeFrames.UserHolder(actor);
        var module = f.Wired.Variables.Module;

        Assert.False(module.Mutate(habbicon, holder, WiredVariableMutation.Give, 32, frame));
        Assert.False(module.Mutate(habbicon, holder, WiredVariableMutation.Replace, 32, frame));
        Assert.False(module.Mutate(habbicon, holder, WiredVariableMutation.Set, 33, frame));
        Assert.False(module.Mutate(habbicon, holder, WiredVariableMutation.Remove, 0, frame));
        Assert.Null(module.Read(habbicon, holder, frame));
    }
}
