using System.Buffers.Binary;
using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// The room owner is admitted through the real RoomUserManager owner branch. The recorded official owner status reads "flatctrl 4" (the same level the
// controller packet already carries); these tests only compare the status string, not any client visual, permission or wf behaviour.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void RealOwnerAdmissionSendsTheOwnerControllerLevelFour()
    {
        var actor = AdmitOwner();

        Assert.NotNull(actor);
        Assert.Contains(ServerPacketHeader.YouAreOwnerComposer, _client.Sent);
        var controller = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.YouAreControllerComposer);
        var body = new FlashIncomingPacket { Buffer = controller.Body };
        Assert.Equal(RoomId, body.ReadUInt());
        Assert.Equal(4, body.ReadInt());
        Assert.False(body.HasDataRemaining());
    }

    [Fact]
    public void RealOwnerAdmissionStatusSnapshotCarriesFlatctrlFour()
    {
        var actor = AdmitOwner();

        var snapshot = Assert.Single(RoomUserStatusSnapshot.Capture([actor]));

        Assert.Equal("/flatctrl 4//", snapshot.Status);
    }

    [Fact]
    public void RealOwnerAdmissionUserUpdateWireCarriesFlatctrlFourToTheExactEnd()
    {
        var actor = AdmitOwner();

        var users = ReadUserUpdateWire(TypedFurnitureStateTests.Payload(new UserUpdateComposer(RoomUserStatusSnapshot.Capture([actor]))));

        var user = Assert.Single(users);
        Assert.Equal(actor.VirtualId, user.Index);
        Assert.Equal(0, user.Third);
        Assert.Equal("/flatctrl 4//", user.Status);
    }

    // Controls through the same real admission: a rights holder keeps flatctrl 1 with controller level 1, a visitor has no flatctrl status.
    [Fact]
    public void RealRightsHolderAdmissionKeepsFlatctrlOne()
    {
        _room.UsersWithRights = [8];
        var holder = new TestClient();
        var actor = Admit(holder, 8, "guest");

        Assert.Equal("/flatctrl 1//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        var controller = Assert.Single(holder.Packets, packet => packet.Header == ServerPacketHeader.YouAreControllerComposer);
        var body = new FlashIncomingPacket { Buffer = controller.Body };
        Assert.Equal(RoomId, body.ReadUInt());
        Assert.Equal(1, body.ReadInt());
    }

    [Fact]
    public void RealVisitorAdmissionHasNoFlatctrlStatus()
    {
        _room.UsersWithRights = [8];
        var visitor = new TestClient();
        var actor = Admit(visitor, 9, "visitor");

        Assert.Equal("//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        Assert.Contains(ServerPacketHeader.YouAreNotControllerComposer, visitor.Sent);
        Assert.DoesNotContain(ServerPacketHeader.YouAreControllerComposer, visitor.Sent);
    }

    private RoomUser AdmitOwner() => Admit(_client, 7, null);

    // A real manager and the real AddAvatarToRoom, as the other admission tests in this fixture do.
    private RoomUser Admit(TestClient client, int habboId, string? username)
    {
        var manager = new RoomUserManager(_room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(),
            TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel);
        Set("_roomUserManager", manager);
        _room.WordFilterList = [];

        if (username != null) {
            client.SetHabbo(new Habbo { Id = habboId, Username = username, CurrentRoom = _room, Access = UserAccess.Empty });
        }

        client.GetHabbo().IgnoresComponent = new([]);
        client.Sent.Clear();
        client.Packets.Clear();
        Assert.True(manager.AddAvatarToRoom(client));

        return Assert.IsType<RoomUser>(manager.GetRoomUserByHabbo(habboId));
    }

    private sealed record WireUser(int Index, int Third, string Status);

    // The supported client's user update grammar: count; per user index, x, y, z string, head, body, third i32, status string; exact end.
    private static List<WireUser> ReadUserUpdateWire(byte[] bytes)
    {
        var position = 0;
        int Int()
        {
            Assert.True(position + 4 <= bytes.Length, $"i32 at {position} runs past the {bytes.Length}-byte payload");
            var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
            position += 4;

            return value;
        }
        string String()
        {
            Assert.True(position + 2 <= bytes.Length, $"string length at {position} runs past the {bytes.Length}-byte payload");
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
            Assert.True(position + 2 + length <= bytes.Length, $"string of {length} bytes at {position} runs past the {bytes.Length}-byte payload");
            var value = Encoding.UTF8.GetString(bytes, position + 2, length);
            position += 2 + length;

            return value;
        }

        var users = new List<WireUser>();
        var count = Int();

        for (var i = 0; i < count; i++) {
            var index = Int();
            _ = Int();
            _ = Int();
            _ = String();
            _ = Int();
            _ = Int();
            var third = Int();
            users.Add(new(index, third, String()));
        }

        Assert.Equal(bytes.Length, position);

        return users;
    }
}
