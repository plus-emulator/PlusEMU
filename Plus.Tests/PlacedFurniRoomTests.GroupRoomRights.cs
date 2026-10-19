using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610151745-318700621 (AvatarMenuView.isShowGiveRights): the owner of a group home room can give rights
// to any avatar below controller level 1, so a group room keeps individual rights holders at level 1.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void RightsHolderEnteringAGroupRoomGetsGuestControllerLevel()
    {
        _room.Group = GroupWithAdmins();
        _room.UsersWithRights = [8];
        var holder = new TestClient();
        var actor = Admit(holder, 8, "guest");

        Assert.Equal("/flatctrl 1//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        Assert.Equal(1, ControllerLevelSentTo(holder));
    }

    [Fact]
    public void GroupAdminEnteringAGroupRoomKeepsGroupAdminControllerLevel()
    {
        _room.Group = GroupWithAdmins(9);
        _room.UsersWithRights = [];
        var admin = new TestClient();
        var actor = Admit(admin, 9, "admin");

        Assert.Equal("/flatctrl 3//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        Assert.Equal(3, ControllerLevelSentTo(admin));
    }

    [Fact]
    public void OwnerGivingRightsInAGroupRoomMakesTheAvatarAController()
    {
        _room.Group = GroupWithAdmins();
        _room.UsersWithRights = [];
        var stored = new List<int>();
        var store = Proxy<IRoomRightsStore>((method, args) =>
        {
            Assert.Equal("Assign", method);
            stored.Add((int)args[1]!);

            return null;
        });
        var rights = new RoomRightsService(store, null!, Proxy<ICacheManager>((_, _) => null));
        var holder = new TestClient();
        var actor = Admit(holder, 8, "guest");
        holder.Packets.Clear();

        rights.Assign(_room, _client, 8);

        Assert.Equal([8], stored);
        Assert.Equal([8], _room.UsersWithRights);
        Assert.Equal("/flatctrl 1//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        Assert.Equal(1, ControllerLevelSentTo(holder));
    }

    private static Group GroupWithAdmins(params int[] admins) =>
        new(20, "group", "", "", RoomId, 7, null, 0, 1, 1, 0, false, new GroupMembershipSnapshot([], [.. admins], []));

    private static int ControllerLevelSentTo(TestClient client)
    {
        var controller = Assert.Single(client.Packets, packet => packet.Header == ServerPacketHeader.YouAreControllerComposer);
        var body = new FlashIncomingPacket { Buffer = controller.Body };
        Assert.Equal(RoomId, body.ReadUInt());
        var level = body.ReadInt();
        Assert.False(body.HasDataRemaining());

        return level;
    }
}
