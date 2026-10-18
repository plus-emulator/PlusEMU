using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621: a plain room entry receives the entering user's status once, from the room tick.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void RoomEntrySendsTheEnteringUsersStatusOnce()
    {
        AdmitOwner();
        _client.Packets.Clear();

        _room.SendObjects(_client);
        _room.GetRoomUserManager().SerializeStatusUpdates();
        _room.GetRoomUserManager().SerializeStatusUpdates();

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
    }

    [Fact]
    public void RoomEntrySnapshotStillCarriesSettledUsers()
    {
        var owner = AdmitOwner();
        _room.GetRoomUserManager().SerializeStatusUpdates();
        var visitor = new TestClient();
        visitor.SetHabbo(new Habbo { Id = 9, Username = "visitor", CurrentRoom = _room, Access = UserAccess.Empty });
        visitor.GetHabbo().IgnoresComponent = new([]);
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(visitor));
        visitor.Packets.Clear();

        _room.SendObjects(visitor);

        var snapshot = Assert.Single(visitor.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
        var user = Assert.Single(ReadUserUpdateWire(snapshot.Body));
        Assert.Equal(owner.VirtualId, user.Index);
    }
}
