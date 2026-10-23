using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// When Kick User removes a visitor, the visitor gets YouArePlayingGame(false) and then CloseConnection with the short 2. No GenericError is sent.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void KickClosesTheRoomWithReasonTwoAndNoGenericError()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Advance(2000);

        Assert.False(f.User.IsAttachedTo(f.Room));
        Assert.DoesNotContain(replies, reply => reply.Header == ServerPacketHeader.GenericErrorComposer);
        var playing = replies.FindIndex(reply => reply.Header == ServerPacketHeader.YouArePlayingGameComposer);
        var close = replies.FindIndex(reply => reply.Header == ServerPacketHeader.CloseConnectionComposer);
        Assert.True(playing >= 0 && close > playing, string.Join(",", replies.Select(reply => reply.Header)));
        Assert.Equal(new byte[] { 0, 2 }, replies[close].Body);
    }
}
