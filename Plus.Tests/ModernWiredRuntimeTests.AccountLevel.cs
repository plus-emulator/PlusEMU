using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Permissions;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // The room stats answer caps the execution cost at 312.5 x (room owner's account level + 2), with the permanent
    // variable caps that follow it. The viewer's own level does not count.
    [Fact]
    public async Task TheRoomStatsCapFollowsTheRoomOwnersAccountLevelNotTheViewers()
    {
        using var f = new TeleportFixture();
        f.Room.OwnerName = "Bob";
        f.Room.OwnerId = 2;
        f.Room.Type = "private";
        f.Room.UsersWithRights = [];
        f.Habbo.AccountLevel = 13;
        f.Habbo.Access = EditorTestSupport.Access([PermissionKeys.RoomRightsAny]);
        var alice = Capture(f.Client);

        f.Room.Data.OwnerAccountLevel = 6;
        Assert.Equal((2500d, 12, 50, 37), await RequestCaps(f, alice));

        f.Room.Data.OwnerAccountLevel = 9;
        Assert.Equal((3437.5, 17, 68, 51), await RequestCaps(f, alice));
        Assert.Empty(alice);
    }

    private static async Task<(double Cap, int Furni, int User, int Global)> RequestCaps(TeleportFixture f, List<(uint Header, byte[] Body)> replies)
    {
        var monitor = new WiredMonitorService(new WiredRequestGateService(new ManualMonotonicClock()));
        await new GetWiredRoomStatsEvent(monitor).Parse(f.Room, f.Client, Request());
        var stats = Reply(replies, ServerPacketHeader.WiredRoomStatsComposer);
        stats.Skip(2);
        var cap = BitConverter.Int64BitsToDouble(((long)stats.Int() << 32) | (uint)stats.Int());
        stats.Bool();
        stats.Skip(4);
        var furni = (stats.Int(), stats.Int());
        var user = (stats.Int(), stats.Int());
        var global = (stats.Int(), stats.Int());
        stats.End();

        return (cap, furni.Item2, user.Item2, global.Item2);
    }
}
