using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Runtime;
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

    // The wired.execution_cost_cap hotel setting replaces the owner-level cap in the room stats answer.
    [Fact]
    public async Task TheHotelCapSettingOverridesTheOwnersLevelInTheRoomStats()
    {
        using var f = new TeleportFixture();
        f.Room.OwnerName = "Bob";
        f.Room.OwnerId = 2;
        f.Room.Type = "private";
        f.Room.UsersWithRights = [];
        f.Habbo.Access = EditorTestSupport.Access([PermissionKeys.RoomRightsAny]);
        f.Room.Data.OwnerAccountLevel = 6;
        typeof(WiredStackEngine).GetField("_limits", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(f.Engine, WiredEngineLimits.FromSettings(key => key == "wired.execution_cost_cap" ? "4687.5" : "0"));

        Assert.Equal((4687.5, 23, 93, 70), await RequestCaps(f, Capture(f.Client)));
    }

    // User Level compares the actor's account level, whatever the achievement score.
    [Theory]
    [InlineData(10, 0, true)]
    [InlineData(9, 0, false)]
    [InlineData(9, 1, true)]
    [InlineData(8, 1, false)]
    [InlineData(8, 2, true)]
    [InlineData(9, 2, false)]
    public void UserLevelComparesTheAccountLevel(int level, int comparison, bool passes)
    {
        using var f = new TeleportFixture();
        f.Habbo.HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0) { AchievementPoints = 25000 };
        f.Habbo.AccountLevel = 9;
        var item = MakeItem(101, "wf_cnd_user_level");
        var descriptor = Descriptor("wf_cnd_user_level");
        var condition = new WiredModernCondition(f.Room, item, descriptor, TestGroupManager.Empty, _ => null, () => DateTimeOffset.UnixEpoch);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [level, comparison] };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, descriptor, native, out var proposed));
        Assert.True(condition.TryValidateConfiguration(proposed, out var configuration, out var error), error);
        condition.ApplyConfiguration(configuration);

        var context = Context(f.Room, new(WiredEventKind.Use) { Actor = f.User }, [], [f.User]);
        context.Triggering.UserIds.Add(f.User.VirtualId);

        Assert.Equal(passes, condition.Execute(context));
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
