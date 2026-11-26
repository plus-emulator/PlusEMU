using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// An effect ends a dance: the room gets Dance 0 before the effect, and the server no longer counts the avatar as dancing.
public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData(102)]
    [InlineData(0)]
    public void AnEffectEndsTheDanceForTheRoomAndTheServer(int effect)
    {
        var room = new ExpressionRoom();
        room.Dance(1);
        room.Wait(1000);
        room.Frames.Clear();

        room.Owner.GetClient()!.GetHabbo().Effects!.ApplyEffect(effect);

        Assert.Equal([ServerPacketHeader.DanceComposer, ServerPacketHeader.AvatarEffectComposer], room.Frames.Select(frame => frame.Header).ToArray());
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(room.Frames[0].Payload.AsSpan(4)));
        Assert.False(room.Owner.IsDancing);
        Assert.Equal(0, room.Owner.DanceId);
    }

    [Fact]
    public void AnAvatarCanDanceAgainAfterAnEffectEndedTheDance()
    {
        var room = new ExpressionRoom();
        room.Dance(1);
        room.Wait(1000);
        room.Owner.GetClient()!.GetHabbo().Effects!.ApplyEffect(102);
        room.Frames.Clear();

        room.Dance(1);

        var dance = Assert.Single(room.Frames, frame => frame.Header == ServerPacketHeader.DanceComposer);
        Assert.Equal(1, BinaryPrimitives.ReadInt32BigEndian(dance.Payload.AsSpan(4)));
        Assert.Equal(1, room.Owner.DanceId);
    }
}
