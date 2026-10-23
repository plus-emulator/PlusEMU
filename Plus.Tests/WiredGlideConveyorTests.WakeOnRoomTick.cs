using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rewards;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Fact]
    public void ASleepingAvatarWakesOnTheRoomTickAfterItTalks()
    {
        var f = new Fixture([(ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null)], live: true);
        f.Room.MutedUsers = [];
        var owner = f.Walker(1, 6, 10, 6, 11);
        f.Advance(1000);
        owner.IsAsleep = true;
        var frames = CaptureFlashFrames(owner);

        f.Advance(150);
        Say(f, new ChatEvent(ChatService()), owner, "wake");

        Assert.DoesNotContain(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);
        Assert.True(owner.IsAsleep);

        f.Advance(500);

        var sleep = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);
        Assert.Equal(0, sleep.Payload[4]);
        Assert.False(owner.IsAsleep);
        Assert.True(frames.FindIndex(frame => frame.Header == ServerPacketHeader.ChatComposer) < frames.FindIndex(frame => frame.Header == ServerPacketHeader.SleepComposer));
    }

    [Theory]
    [InlineData("wave")]
    [InlineData("sign")]
    [InlineData("dance")]
    public void ASleepingAvatarWakesOnTheRoomTickAfterItActs(string action)
    {
        var f = new Fixture([(ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null)], live: true);
        var owner = f.Walker(1, 6, 10, 6, 11);
        f.Advance(1000);
        owner.IsAsleep = true;
        var frames = CaptureFlashFrames(owner);
        var service = new RoomAvatarActionService(TimeProvider.System, NoOp<IQuestManager>.Create(), NoOp<IRewardTrackManager>.Create());

        f.Advance(150);
        f.Owned(() =>
        {
            if (action == "wave") {
                service.PerformAction(f.Room, owner.GetClient()!, 1);
            }
            else if (action == "sign") {
                service.ApplySign(f.Room, owner.GetClient()!, 3);
            }
            else {
                service.Dance(f.Room, owner.GetClient()!, 1);
            }
        });

        Assert.DoesNotContain(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);

        f.Advance(500);

        var sleep = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);
        Assert.Equal(0, sleep.Payload[4]);
        Assert.False(owner.IsAsleep);
    }

    [Theory]
    [InlineData(7, 11, true)]
    [InlineData(6, 11, false)]
    public void AWalkWakesASleepingAvatarAtOnceWhenItMovesAndOnTheRoomTickWhenItDoesNot(int x, int y, bool atOnce)
    {
        var f = new Fixture([(ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null)], live: true);
        var owner = f.Walker(1, 6, 10, 6, 11);
        f.Advance(1000);
        owner.IsAsleep = true;
        var frames = CaptureFlashFrames(owner);
        var service = new RoomAvatarActionService(TimeProvider.System, NoOp<IQuestManager>.Create(), NoOp<IRewardTrackManager>.Create());

        f.Advance(150);
        f.Owned(() => service.Move(owner.GetClient()!, x, y));

        Assert.Equal(atOnce, frames.Any(frame => frame.Header == ServerPacketHeader.SleepComposer));

        f.Advance(500);

        var sleep = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);
        Assert.Equal(0, sleep.Payload[4]);
    }

    [Fact]
    public void AShowMessageWakesTheSleepingAvatarOnTheRoomTick()
    {
        var (f, rider, subject) = ClickStackRoom([true]);
        f.Advance(1000);
        rider.IsAsleep = true;
        var frames = CaptureFlashFrames(rider);

        ObservedClickSubject(f, rider, subject);
        f.Advance(100);

        Assert.Contains(frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);
        Assert.DoesNotContain(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);

        f.Advance(500);

        var sleep = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.SleepComposer);
        Assert.Equal(0, sleep.Payload[4]);
        Assert.False(rider.IsAsleep);
    }
}
