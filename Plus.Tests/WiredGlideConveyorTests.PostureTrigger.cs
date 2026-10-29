using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// User Performs Action - Sit and Stand fire with the status update of the room tick that applies the posture, not when the click arrives.
public sealed partial class WiredGlideConveyorTests
{
    private const uint SitTrigger = 700;
    private const uint StandTrigger = 710;

    [Theory]
    [InlineData(50, true)]
    [InlineData(200, true)]
    [InlineData(350, true)]
    [InlineData(450, true)]
    [InlineData(50, false)]
    [InlineData(250, false)]
    [InlineData(450, false)]
    public void SitAndStandFireTheirTriggerWithTheStatusUpdateOfTheNextRoomTick(int clickPhase, bool sit)
    {
        var room = new PostureRoom();
        var action = sit ? WiredAvatarAction.Sit : WiredAvatarAction.Stand;

        if (!sit) {
            room.ChangePosture(1);
            room.Wait(500);
            room.Frames.Clear();
            room.Wait(500);
            room.Frames.Clear();
        }

        room.Wait(clickPhase);
        room.ChangePosture(sit ? 1 : 0);
        room.Wait(500 - clickPhase - 50);

        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);
        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.UserUpdateComposer);

        room.Wait(50);

        Assert.Contains(room.Frames, frame => frame.Header == ServerPacketHeader.UserUpdateComposer);

        room.Wait(100);

        var shown = room.Frames.FindIndex(frame => frame.Header == ServerPacketHeader.WhisperComposer);
        Assert.True(shown >= 0, $"the {action} trigger fires with the room tick");
        Assert.Equal(sit, room.Owner.IsSitting);
        Assert.Contains(sit ? "sit" : "stand", System.Text.Encoding.UTF8.GetString(room.Frames[shown].Payload));
    }

    [Fact]
    public void ASitRequestWhileTheAvatarAlreadySitsFiresNothing()
    {
        var room = new PostureRoom();
        room.ChangePosture(1);
        room.Wait(1000);
        room.Frames.Clear();

        room.ChangePosture(1);
        room.Wait(1000);

        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);
    }

    [Fact]
    public void ASitFollowedByAStandWithinOneRoomTickChangesNothingAndFiresNothing()
    {
        var room = new PostureRoom();

        room.ChangePosture(1);
        room.Wait(150);
        room.ChangePosture(0);
        room.Wait(1000);

        Assert.False(room.Owner.IsSitting);
        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);
        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.UserUpdateComposer);
    }

    [Fact]
    public void AStandFollowedByASitWithinOneRoomTickChangesNothingAndFiresNothing()
    {
        var room = new PostureRoom();
        room.ChangePosture(1);
        room.Wait(1000);
        room.Frames.Clear();

        room.ChangePosture(0);
        room.Wait(150);
        room.ChangePosture(1);
        room.Wait(1000);

        Assert.True(room.Owner.IsSitting);
        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);
        Assert.DoesNotContain(room.Frames, frame => frame.Header == ServerPacketHeader.UserUpdateComposer);
    }

    [Fact]
    public void ASitRequestIsKeptWhileTheAvatarWalksAndTheAvatarSitsWhereTheWalkEnds()
    {
        var room = new PostureRoom();
        room.Owner.MoveTo(9, 10);
        room.Wait(500);
        room.Wait(100);
        room.ChangePosture(1);
        room.Wait(500);

        Assert.True(room.Owner.IsWalking);
        Assert.True(room.Owner.Statusses.ContainsKey("sit"));
        Assert.True(room.Owner.Statusses.ContainsKey("mv"));
        Assert.Single(room.Frames, frame => frame.Header == ServerPacketHeader.WhisperComposer);

        room.Wait(3000);

        Assert.False(room.Owner.IsWalking);
        Assert.Equal((9, 10), (room.Owner.X, room.Owner.Y));
        Assert.True(room.Owner.IsSitting);
        Assert.True(room.Owner.Statusses.ContainsKey("sit"));
    }

    private sealed class PostureRoom
    {
        private readonly Fixture _fixture;
        private readonly RoomAvatarActionService _actions = new(TimeProvider.System,
            CatalogSnapshotTestSupport.Proxy<IQuestManager>((_, _) => null), TestNavigationRewards.Instance);

        public RoomUser Owner { get; }
        public List<(uint Header, byte[] Payload)> Frames { get; }

        public PostureRoom()
        {
            var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
            {
                (SitTrigger, 4, 12, 0.0, 0, "wf_trg_user_performs_action", Performs(WiredAvatarAction.Sit)),
                (SitTrigger + 1, 4, 12, 0.65, 0, "wf_act_show_message", ShownMessage("sit")),
                (StandTrigger, 5, 12, 0.0, 0, "wf_trg_user_performs_action", Performs(WiredAvatarAction.Stand)),
                (StandTrigger + 1, 5, 12, 0.65, 0, "wf_act_show_message", ShownMessage("stand"))
            };
            _fixture = new Fixture(layout, live: true);
            Owner = _fixture.Walker(1, 6, 10, 6, 11);
            _fixture.Advance(1000);
            Frames = CaptureFlashFrames(Owner);
        }

        public void ChangePosture(int posture) => _fixture.Owned(() =>
            new SitEvent(_actions).Parse(Owner.GetClient()!, IntPacket(posture)).GetAwaiter().GetResult());

        public void Wait(int milliseconds) => _fixture.Advance(milliseconds);

        private static string Performs(WiredAvatarAction action) =>
            JsonSerializer.Serialize(new WiredConfiguration { IntParams = [(int)action, 0, 0, 0, 1] });

        private static FlashIncomingPacket IntPacket(int value)
        {
            var body = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(body, value);

            return new() { Buffer = body };
        }
    }
}
