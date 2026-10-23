using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

// A user in a Wired team (effect 223) gets 223 back on unfreeze.
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
        // The effect arrives with the next room tick.
        Assert.Equal(before, f.Habbo.Effects!.CurrentEffect);
        state.FlushTickEffects();
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);
        Assert.True(state.FreezeUser(f.User, 218, false)); // A second freeze keeps the effect worn before the first one.
        state.FlushTickEffects();
        Assert.True(state.Thaw(f.User));
        state.FlushTickEffects();
        Assert.Equal(after, f.Habbo.Effects!.CurrentEffect);
    }

    // Freeze and unfreeze of a standing user send no UserUpdate.
    [Fact]
    public void FreezeAndThawFlagAStatusUpdateOnlyForAWalkingUser()
    {
        var (room, _, _) = World();
        var user = new RoomUser(1, 0, 7, room, null, TestChatEmotions.Unused, TestRewardProgress.Unused);
        var state = new WiredAvatarState();
        user.UpdateNeeded = false;
        Assert.True(state.FreezeUser(user, 0, false));
        Assert.False(user.UpdateNeeded);
        Assert.True(state.Thaw(user));
        Assert.False(user.UpdateNeeded);
        user.SetStatus("mv", "1,1,0");
        user.IsWalking = true;
        user.UpdateNeeded = false;
        Assert.True(state.FreezeUser(user, 0, false));
        Assert.True(user.UpdateNeeded);
        Assert.False(user.HasStatus("mv"));
    }

    // A teleport that cancels a freeze sends only the teleport effect, so the freeze
    // effect is not cleared first.
    [Fact]
    public void ATeleportThatCancelsAFreezeLeavesTheEffectToTheTeleport()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        var state = new WiredAvatarState();
        Assert.True(state.FreezeUser(f.User, 218, true));
        Assert.True(state.Thaw(f.User, teleport: true));
        state.FlushTickEffects();
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);
        Assert.False(f.User.Frozen);
    }

    // When the teleport that cancelled a freeze ends, the user gets back the effect worn
    // before the freeze (223 in a Wired team, otherwise 0), not the freeze effect.
    [Theory]
    [InlineData(223)]
    [InlineData(0)]
    public void ATeleportThatCancelsAFreezeEndsWithTheEffectWornBeforeTheFreeze(int before)
    {
        using var f = new TeleportFixture();
        GiveUserAccess(f);
        f.User.ApplyEffect(before);
        var state = WiredAvatarState.For(f.Room);

        using (RoomOwnerScope.Enter(f.Room)) {
            Assert.True(state.FreezeUser(f.User, 163, true));
        }

        state.FlushTickEffects();
        Assert.Equal(163, f.Habbo.Effects!.CurrentEffect);
        f.Fire();
        Assert.Equal(4, f.Habbo.Effects!.CurrentEffect);
        f.Advance(2000);
        state.FlushTickEffects();
        Assert.Equal(before, f.Habbo.Effects!.CurrentEffect);
        Assert.False(f.User.Frozen);
        Assert.Empty(f.Errors);
    }

    // A teamed user re-entering the room gets no avatar
    // effect back, also when the session left before the room let go of it.
    [Fact]
    public void LeavingTheRoomDropsTheTeamEffectEvenWhenTheSessionMovedOn()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(226);
        f.User.Team = Plus.HabboHotel.Rooms.Games.Teams.Team.Green;
        f.Habbo.CurrentRoom = null;
        f.Room.GetRoomUserManager().RemoveUserFromRoom(f.Client, false, false);
        Assert.Equal(0, f.Habbo.Effects!.CurrentEffect);
    }
}
