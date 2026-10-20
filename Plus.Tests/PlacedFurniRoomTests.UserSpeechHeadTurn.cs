using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621 with two avatars: a user's line turns another standing avatar's head one step
// toward the speaker on the next status update and back about 5000 ms later; with the speaker straight behind, nothing turns.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AUsersLineTurnsAnotherStandingAvatarsHeadOneStepTowardTheSpeaker(bool shout)
    {
        var (speaker, listener) = TwoAvatars(listenerBody: 0);

        speaker.OnChat(0, "hello there", shout);

        Assert.Equal((1, 0), NextHeadAndBody(listener));
        Assert.Equal(speaker.RotBody, speaker.RotHead);
    }

    [Fact]
    public void AUsersHeadGoesBackToTheBodyFiveSecondsAfterTheTurn()
    {
        var (speaker, listener) = TwoAvatars(listenerBody: 0);
        speaker.OnChat(0, "hello there", false);
        Assert.Equal((1, 0), NextHeadAndBody(listener));

        for (var cycle = 1; cycle < 10; cycle++) {
            Assert.Null(NextHeadAndBody(listener));
        }

        Assert.Equal((0, 0), NextHeadAndBody(listener));
    }

    [Fact]
    public void AUserStraightBehindTurnsNoHead()
    {
        var (speaker, listener) = TwoAvatars(listenerBody: 6);

        speaker.OnChat(0, "hello there", false);

        Assert.Null(NextHeadAndBody(listener));
        Assert.Equal(6, listener.RotHead);
    }

    [Fact]
    public void TheSpeakingUserKeepsItsOwnHead()
    {
        var (speaker, _) = TwoAvatars(listenerBody: 0);
        speaker.RotBody = speaker.RotHead = 6;

        speaker.OnChat(0, "hello there", false);

        Assert.Null(NextHeadAndBody(speaker));
        Assert.Equal(6, speaker.RotHead);
    }

    // The listener stands at (1, 2) and the speaker three tiles east at (4, 2), facing west.
    private (RoomUser Speaker, RoomUser Listener) TwoAvatars(int listenerBody)
    {
        var manager = new RoomUserManager(_room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(),
            new TestChatEmotions(_ => 0), TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel);
        Set("_roomUserManager", manager);
        _room.WordFilterList = [];
        _client.GetHabbo().IgnoresComponent = new([]);
        Assert.True(manager.AddAvatarToRoom(_client));
        var listener = Assert.IsType<RoomUser>(manager.GetRoomUserByHabbo(7));
        var visitor = new TestClient();
        visitor.SetHabbo(new Habbo { Id = 9, Username = "visitor", CurrentRoom = _room, Access = UserAccess.Empty });
        visitor.GetHabbo().IgnoresComponent = new([]);
        Assert.True(manager.AddAvatarToRoom(visitor));
        var speaker = Assert.IsType<RoomUser>(manager.GetRoomUserByHabbo(9));
        (listener.X, listener.Y, listener.RotBody, listener.RotHead) = (1, 2, listenerBody, listenerBody);
        (speaker.X, speaker.Y, speaker.RotBody, speaker.RotHead) = (4, 2, 6, 6);
        NextHeadAndBody(listener);

        return (speaker, listener);
    }

    // One room tick of head turns and the status update after it, as the listener's client reads it.
    private (int Head, int Body)? NextHeadAndBody(RoomUser unit)
    {
        _client.Packets.Clear();
        _room.GetRoomUserManager().CycleHeadTurns();
        _room.GetRoomUserManager().SerializeStatusUpdates();

        foreach (var sent in _client.Packets.Where(packet => packet.Header == ServerPacketHeader.UserUpdateComposer)) {
            var packet = new FlashIncomingPacket { Buffer = sent.Body };
            var count = packet.ReadInt();

            for (var i = 0; i < count; i++) {
                var id = packet.ReadInt();
                packet.ReadInt();
                packet.ReadInt();
                packet.ReadString();
                var (head, body) = (packet.ReadInt(), packet.ReadInt());
                packet.ReadInt();
                packet.ReadString();

                if (id == unit.VirtualId) {
                    return (head, body);
                }
            }
        }

        return null;
    }
}
