using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): Kick User on the room owner whispers "Wired kick exception: Room owner" (style 34) and kicks nobody.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void KickOnTheRoomOwnerWhispersTheExceptionAndKicksNobody()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "kick probe");
        GiveUserAccess(f);
        f.Room.OwnerId = 1;
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Advance(2000);

        var chat = Assert.Single(Chats(replies));
        Assert.Equal(WiredModernAction.KickOwnerException, chat.Text);
        Assert.Equal(34, chat.Bubble);
        Assert.Equal(Plus.Communication.Packets.Outgoing.ServerPacketHeader.WhisperComposer, chat.Header);
        Assert.True(f.User.IsAttachedTo(f.Room));
        Assert.Empty(f.Errors);
    }
}
