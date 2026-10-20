using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: while a user is frozen, Join Team and Leave Team change the team and send
// YouArePlayingGame, but the user keeps the freeze effect. Unfreeze then puts back the effect of the current team, or 0 without a team.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void JoinAndLeaveTeamWhileFrozenKeepTheFreezeEffectAndUnfreezeShowsNoTeam()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(0);
        var sent = Capture(f.Client);

        Run(f, "wf_act_freeze");
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);
        sent.Clear();

        Run(f, "wf_act_join_team", [1, 0]);
        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer], sent.Select(packet => packet.Header));
        Assert.True(Reply(sent, ServerPacketHeader.YouArePlayingGameComposer).Bool());
        Assert.Equal(Team.Red, f.User.Team);
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_leave_team");
        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer], sent.Select(packet => packet.Header));
        Assert.False(Reply(sent, ServerPacketHeader.YouArePlayingGameComposer).Bool());
        Assert.Equal(Team.None, f.User.Team);
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_unfreeze");
        Assert.Equal(0, f.Habbo.Effects!.CurrentEffect);
    }

    [Fact]
    public void UnfreezeShowsTheTeamJoinedWhileFrozen()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(0);

        Run(f, "wf_act_freeze");
        Run(f, "wf_act_join_team", [1, 0]);
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_unfreeze");
        Assert.Equal(223, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_leave_team");
        Assert.Equal(0, f.Habbo.Effects!.CurrentEffect);
    }

    // Observed on AIR WIN63-202610151745-318700621: a frozen user joining a Battle Banzai or Freeze game team, and leaving it, gets only
    // YouArePlayingGame and keeps the freeze effect, like a Wired team.
    [Theory]
    [InlineData(4, 1)]
    [InlineData(1, 2)]
    public void JoinAndLeaveAGameTeamWhileFrozenKeepTheFreezeEffect(int team, int type)
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(0);
        var sent = Capture(f.Client);

        Run(f, "wf_act_freeze");
        sent.Clear();

        Run(f, "wf_act_join_team", [team, type]);
        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer], sent.Select(packet => packet.Header));
        Assert.True(Reply(sent, ServerPacketHeader.YouArePlayingGameComposer).Bool());
        Assert.Equal((Team)team, f.User.Team);
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_leave_team");
        Assert.Equal([ServerPacketHeader.YouArePlayingGameComposer], sent.Select(packet => packet.Header));
        Assert.False(Reply(sent, ServerPacketHeader.YouArePlayingGameComposer).Bool());
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_unfreeze");
        Assert.Equal(0, f.Habbo.Effects!.CurrentEffect);
    }

    // Inferred from the observed Wired team case; no Habbo trace shows unfreeze inside a Battle Banzai or Freeze team yet.
    [Theory]
    [InlineData(4, 1, 36)]
    [InlineData(1, 2, 40)]
    public void UnfreezeShowsTheGameTeamJoinedWhileFrozen(int team, int type, int effect)
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        f.User.ApplyEffect(0);

        Run(f, "wf_act_freeze");
        Run(f, "wf_act_join_team", [team, type]);
        Assert.Equal(218, f.Habbo.Effects!.CurrentEffect);

        Run(f, "wf_act_unfreeze");
        Assert.Equal(effect, f.Habbo.Effects!.CurrentEffect);
    }

    private static void Run(TeleportFixture f, string name, int[]? ints = null)
    {
        var box = ActionBox(f.Room, name);
        var native = WiredNativeEditorProjection.DefaultNative(Descriptor(name));
        WiredNativeTestSupport.Install(box, ints == null ? native : native with { OwnedIntParams = [.. ints] });
        var context = Context(f.Room, new(WiredEventKind.Use) { Actor = f.User }, f.Items.Values.ToArray(), [f.User]);
        context.Triggering.UserIds.Add(f.User.VirtualId);
        box.Execute(context);
        f.Room.GetGameMap().Navigation.DrainCommands();
        WiredAvatarState.For(f.Room).FlushTickEffects();
    }
}
