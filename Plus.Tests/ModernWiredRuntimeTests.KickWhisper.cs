using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: Kick User whispers the kicked user (an empty text when the box has none) before the
// kick effect, and the user leaves the room 2000 ms after the firing, with or without a message.
public partial class ModernWiredRuntimeTests
{
    [Theory]
    [InlineData("", "")]
    [InlineData("bye", "bye")]
    public void KickWhispersTheUserBeforeTheEffectEvenWithoutAMessage(string text, string whispered)
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: text);
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Advance(500);

        var whisper = Assert.Single(Chats(replies));
        Assert.Equal((ServerPacketHeader.WhisperComposer, whispered, 34), (whisper.Header, whisper.Text, whisper.Bubble));
        Assert.True(replies.FindIndex(reply => reply.Header == ServerPacketHeader.WhisperComposer)
            < replies.FindIndex(reply => reply.Header == ServerPacketHeader.AvatarEffectComposer));
        f.Advance(1500);
        Assert.False(f.User.IsAttachedTo(f.Room));
    }
}
