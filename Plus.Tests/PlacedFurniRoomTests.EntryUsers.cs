using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: room entry sends the users already present as one Users packet before the furni,
// the entrant's own Users entry after the wall items, and no status update for the entrant until the next room cycle.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void EntryAloneSendsAnEmptyUsersListThenTheEntrantOnceAndNoStatusUpdate()
    {
        EnterForNotice();

        var sent = _client.Sent.ToList();
        var users = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.UsersComposer).Select(packet => UsersIndexes(packet.Body)).ToArray();

        Assert.Equal(2, users.Length);
        Assert.Empty(users[0]);
        var entrant = Assert.IsType<HabboHotel.Rooms.RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(7));
        Assert.Equal([entrant.VirtualId], users[1]);
        Assert.DoesNotContain(ServerPacketHeader.UserUpdateComposer, sent);
        Assert.True(sent.IndexOf(ServerPacketHeader.UsersComposer) < sent.IndexOf(ServerPacketHeader.ObjectsComposer));
        Assert.True(sent.LastIndexOf(ServerPacketHeader.UsersComposer) > sent.IndexOf(ServerPacketHeader.ItemsComposer));
        Assert.True(entrant.UpdateNeeded);
    }

    [Fact]
    public void EntryWithAUserPresentListsOnlyThatUserBeforeTheFurniAndUpdatesOnlyItsStatus()
    {
        var guest = new TestClient();
        var present = Admit(guest, 8, "guest");
        _room.GetRoomUserManager().SerializeStatusUpdates();
        guest.Sent.Clear();
        guest.Packets.Clear();

        EnterForNotice();

        var entrant = Assert.IsType<HabboHotel.Rooms.RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(7));
        var users = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.UsersComposer).Select(packet => UsersIndexes(packet.Body)).ToArray();
        Assert.Equal(2, users.Length);
        Assert.Equal([present.VirtualId], users[0]);
        Assert.Equal([entrant.VirtualId], users[1]);
        var update = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer).Body };
        Assert.Equal(1, update.ReadInt());
        Assert.Equal(present.VirtualId, update.ReadInt());

        // The user already in the room learns about the entrant once, from the admission broadcast.
        var broadcast = Assert.Single(guest.Packets, packet => packet.Header == ServerPacketHeader.UsersComposer);
        Assert.Equal([entrant.VirtualId], UsersIndexes(broadcast.Body));
    }

    // Users: count; per user id, name, motto, figure, room index, ...; only the room indexes are read here.
    private static int[] UsersIndexes(byte[] body)
    {
        var packet = new FlashIncomingPacket { Buffer = body };
        var count = packet.ReadInt();

        if (count == 0) {
            Assert.False(packet.HasDataRemaining());

            return [];
        }

        Assert.Equal(1, count);
        packet.ReadInt();
        packet.ReadString();
        packet.ReadString();
        packet.ReadString();

        return [packet.ReadInt()];
    }
}
