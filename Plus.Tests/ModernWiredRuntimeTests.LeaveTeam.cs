using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // Observed on AIR WIN63-202610151745-318700621: Leave Team tells the user it is not playing every time, also outside a team; Join
    // Team into the team the user is already in sends nothing.
    [Fact]
    public void LeaveTeamTellsTheUserItIsNotPlayingEvenOutsideATeam()
    {
        using var f = new TeleportFixture();
        var sent = Capture(f.Client);
        var leave = ActionBox(f.Room, "wf_act_leave_team");
        WiredNativeTestSupport.Install(leave, WiredNativeEditorProjection.DefaultNative(Descriptor("wf_act_leave_team")));

        var context = Context(f.Room, new(WiredEventKind.Use) { Actor = f.User }, f.Items.Values.ToArray(), [f.User]);
        context.Triggering.UserIds.Add(f.User.VirtualId);

        leave.Execute(context);

        Assert.False(Reply(sent, ServerPacketHeader.YouArePlayingGameComposer).Bool());
    }
}
