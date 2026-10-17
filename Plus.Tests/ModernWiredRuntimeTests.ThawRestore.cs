using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): a user in a Wired team (effect 223) gets 223 back on unfreeze.
public partial class ModernWiredRuntimeTests
{
    [Theory]
    [InlineData(223, 223)]
    [InlineData(0, 0)]
    public void UnfreezePutsBackTheEffectWornBeforeTheFreeze(int before, int after)
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(before);
        var state = new WiredAvatarState();
        Assert.True(state.FreezeUser(f.User, 218, false));
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);
        Assert.True(state.FreezeUser(f.User, 218, false)); // A second freeze keeps the effect worn before the first one.
        Assert.True(state.Thaw(f.User));
        Assert.Equal(after, f.Habbo.Effects!.CurrentEffect);
    }
}
