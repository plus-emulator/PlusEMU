using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("remove", false)]
    [InlineData("removeAll", false)]
    [InlineData("removeOwn", false)]
    [InlineData("remove", true)]
    [InlineData("removeAll", true)]
    [InlineData("removeOwn", true)]
    public void RevokingRoomRightsClearsStoredSessionAndBroadcastRights(string operation, bool persistedOnEntry)
    {
        var (rights, holder, actor, stored) = AdmitRightsHolder(persistedOnEntry);

        RevokeRights(rights, holder, operation);
        _room.GetRoomUserManager().SerializeStatusUpdates();

        Assert.Empty(stored);
        Assert.DoesNotContain(8, _room.UsersWithRights);
        var controller = Assert.Single(holder.Packets, packet => packet.Header is
            ServerPacketHeader.YouAreControllerComposer or ServerPacketHeader.YouAreNotControllerComposer);
        var body = new FlashIncomingPacket { Buffer = controller.Body };
        Assert.Equal(RoomId, body.ReadUInt());

        if (controller.Header == ServerPacketHeader.YouAreControllerComposer) {
            Assert.Equal(0, body.ReadInt());
        }

        Assert.False(body.HasDataRemaining());
        var update = Assert.Single(holder.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
        var user = Assert.Single(ReadUserUpdateWire(update.Body));
        Assert.Equal(actor.VirtualId, user.Index);
        Assert.Equal("//", user.Status);
    }

    [Theory]
    [InlineData("remove")]
    [InlineData("removeAll")]
    [InlineData("removeOwn")]
    public void RegrantingRoomRightsAfterEntryBroadcastsExactlyOneGuestControllerStatus(string operation)
    {
        var (rights, holder, actor, stored) = AdmitRightsHolder(true);
        RevokeRights(rights, holder, operation);
        _room.GetRoomUserManager().SerializeStatusUpdates();
        holder.Packets.Clear();

        rights.Assign(_room, _client, 8);
        _room.GetRoomUserManager().SerializeStatusUpdates();

        Assert.Equal(8, Assert.Single(stored));
        Assert.Equal(8, Assert.Single(_room.UsersWithRights));
        var controller = Assert.Single(holder.Packets, packet => packet.Header == ServerPacketHeader.YouAreControllerComposer);
        var body = new FlashIncomingPacket { Buffer = controller.Body };
        Assert.Equal(RoomId, body.ReadUInt());
        Assert.Equal(1, body.ReadInt());
        Assert.False(body.HasDataRemaining());
        var update = Assert.Single(holder.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
        var user = Assert.Single(ReadUserUpdateWire(update.Body));
        Assert.Equal(actor.VirtualId, user.Index);
        Assert.Equal("/flatctrl 1//", user.Status);
    }

    private (RoomRightsService Rights, TestClient Holder, RoomUser Actor, HashSet<int> Stored) AdmitRightsHolder(bool persistedOnEntry)
    {
        var stored = new HashSet<int>();

        if (persistedOnEntry) {
            stored.Add(8);
        }

        _room.UsersWithRights = stored.ToList();
        var holder = new TestClient();
        var actor = Admit(holder, 8, "guest");
        var store = Proxy<IRoomRightsStore>((method, args) =>
        {
            Assert.Equal(RoomId, (uint)args[0]!);

            if (method == "Assign") {
                stored.Add((int)args[1]!);
            }
            else {
                Assert.Equal("Remove", method);
                stored.ExceptWith((IReadOnlyList<int>)args[1]!);
            }

            return null;
        });
        var rights = new RoomRightsService(store, null!, Proxy<ICacheManager>((_, _) => null));

        if (!persistedOnEntry) {
            rights.Assign(_room, _client, 8);
        }

        Assert.Equal("/flatctrl 1//", Assert.Single(RoomUserStatusSnapshot.Capture([actor])).Status);
        _room.GetRoomUserManager().SerializeStatusUpdates();
        holder.Packets.Clear();

        return (rights, holder, actor, stored);
    }

    private void RevokeRights(RoomRightsService rights, TestClient holder, string operation)
    {
        switch (operation) {
            case "remove":
                rights.Remove(_room, _client, [8]);
                break;
            case "removeAll":
                rights.RemoveAll(_room, _client);
                break;
            case "removeOwn":
                rights.RemoveOwn(_room, holder);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }
}
