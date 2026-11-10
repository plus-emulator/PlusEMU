using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// The owner of a group home room can give rights to any avatar below controller level 1, so a group room keeps individual rights holders at level 1.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void BasicMembersBuildOnlyWhenGroupSettingsAllowIt(int adminsOnly, bool allowed)
    {
        var group = GroupWithAdmins();
        group.PublishJoin(8);
        group.AdminOnlyDeco = adminsOnly;
        _room.UsersWithRights = [];
        _room.Group = group;
        var member = new TestClient();
        var actor = Admit(member, 8, "member");
        Assert.Equal(allowed, _room.CheckRights(member, false, true));
        Assert.False(_room.CheckRights(member, true, true));

        if (allowed) {
            Assert.Equal("/flatctrl 1//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
            Assert.Equal(1, ControllerLevelSentTo(member));
        }
        else {
            Assert.False(actor.HasStatus("flatctrl"));
            Assert.DoesNotContain(member.Packets, packet => packet.Header == ServerPacketHeader.YouAreControllerComposer);
        }
    }

    [Fact]
    public void RevocationRemovesGroupRightsButPreservesExplicitRoomRights()
    {
        var group = GroupWithAdmins(8);
        _room.Group = group;
        var member = new TestClient();
        var actor = Admit(member, 8, "member");
        group.TakeAdmin(8);
        member.Packets.Clear();
        _room.GetRoomUserManager().RefreshControllerRights(member);
        Assert.Equal(1, ControllerLevelSentTo(member));
        Assert.Equal("1", actor.Statusses["flatctrl"]);
        group.DeleteMember(8);
        member.Packets.Clear();
        _room.GetRoomUserManager().RefreshControllerRights(member);
        Assert.False(_room.CheckRights(member, false, true));
        Assert.Equal(0, ControllerLevelSentTo(member));
        Assert.False(actor.HasStatus("flatctrl"));
        _room.UsersWithRights = [];
        _room.UsersWithRights.Add(8);
        member.Packets.Clear();
        _room.GetRoomUserManager().RefreshControllerRights(member);
        Assert.Equal(1, ControllerLevelSentTo(member));
        Assert.True(_room.CheckRights(member, false, true));
        Assert.False(_room.CheckRights(member, true, true));
    }

    [Fact]
    public void OwnershipUsesTheAvatarIdAndGroupAdminNeverBecomesRoomOwner()
    {
        _room.Group = GroupWithAdmins(8);
        _room.UsersWithRights = [];
        var admin = new TestClient();
        Admit(admin, 8, _room.OwnerName);
        Assert.False(_room.CheckRights(admin, true, true));
        Assert.True(_room.CheckRights(admin, false, true));
        _client.GetHabbo().Username = "renamed_owner";
        Assert.True(_room.CheckRights(_client, true, true));
    }

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
