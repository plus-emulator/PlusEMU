using System.Drawing;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // Bot Follow stays active when the user leaves and enters again, which gives a new room index.
    [Fact]
    public void BotFollowKeepsFollowingAUserWhoLeavesAndEntersAgain()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Robbie", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Robbie", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(1, 1, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);

        f.Room.GetWired().BeforeActorLeaves(f.User);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
        var back = new RoomUser(1, 0, 12, f.Room, f.Client, TestChatEmotions.Unused, TestRewardProgress.Unused) { X = 4, Y = 4 };
        Admit(f.Room, back);
        back.SetPos(4, 4, 0);

        targets.Poll(f.Room, 0);

        Assert.Contains(Assert.NotNull(WalkGoal(bot)), Around(4, 4));
    }

    [Fact]
    public void BotFollowWaitsWhileTheUserIsAway()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Robbie", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Robbie", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(1, 1, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);

        f.Room.GetWired().BeforeActorLeaves(f.User);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
        targets.Poll(f.Room, 0);

        Assert.True(targets.HasTargets);
        Assert.Null(WalkGoal(bot));
    }

    [Fact]
    public void BotFollowDoesNotFollowADifferentUserWhoEnters()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Robbie", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Robbie", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(1, 1, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);

        f.Room.GetWired().BeforeActorLeaves(f.User);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
        var other = new RoomUser(f.User.HabboId + 1, 0, 12, f.Room, f.Client, TestChatEmotions.Unused, TestRewardProgress.Unused) { X = 4, Y = 4 };
        Admit(f.Room, other);
        other.SetPos(4, 4, 0);

        targets.Poll(f.Room, 0);

        Assert.Null(WalkGoal(bot));
    }
}
