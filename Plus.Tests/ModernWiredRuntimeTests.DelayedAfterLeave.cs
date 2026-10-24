using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>A delayed chain keeps running after the triggering user left, except for effects that need that user.</summary>
public partial class ModernWiredRuntimeTests
{
    private static void LeaveRoom(TeleportFixture f)
    {
        f.Room.GetWired().BeforeActorLeaves(f.User);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
    }

    private static void DelayedToggle(TeleportFixture f)
    {
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_toggle_state", 0, 0, native => native with
        {
            PrimaryItems = [new(f.Target.Id, false)],
            FurniSourceTypes = [100],
            Delay = 4
        });
    }

    private static void DelayedShowMessage(TeleportFixture f)
    {
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_show_message", 0, 0, native => native with
        {
            Text = "hello",
            Delay = 4
        });
    }

    [Fact]
    public void ADelayedToggleStateStillRunsAfterTheTriggeringUserLeft()
    {
        using var f = new TeleportFixture();
        DelayedToggle(f);
        var before = f.Target.LegacyDataString;

        f.Fire(land: false);
        f.Advance(500);
        LeaveRoom(f);
        f.Advance(3000);

        Assert.NotEqual(before, f.Target.LegacyDataString);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ADelayedToggleStateStillRunsWhenTheUserIsGoneWithoutTheLeaveCancel()
    {
        using var f = new TeleportFixture();
        DelayedToggle(f);
        var before = f.Target.LegacyDataString;

        f.Fire(land: false);
        f.Advance(500);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
        f.Advance(3000);

        Assert.NotEqual(before, f.Target.LegacyDataString);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ADelayedToggleStateRunsWhenTheUserStays()
    {
        using var f = new TeleportFixture();
        DelayedToggle(f);
        var before = f.Target.LegacyDataString;

        f.Fire(land: false);
        f.Advance(3500);

        Assert.NotEqual(before, f.Target.LegacyDataString);
    }

    [Fact]
    public void ADelayedSendMessageStillRunsAfterTheTriggeringUserLeft()
    {
        var (f, l1, l2) = LinkTextRoom();
        using var _f = f;
        OctoberBox<WiredModernAction>(f, 111, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [1],
            Text = "late",
            PrimaryItems = [new(l1.Id, false)],
            FurniSourceTypes = [100],
            Delay = 16
        });
        LinkTextReceiveMessage(f, l2);
        LinkTextLog(f, "got [$(trigger_text)]");

        f.Fire(land: false);
        f.Advance(700);
        LeaveRoom(f);
        f.Advance(8000);

        Assert.Equal(["got [late]"], LinkTextLogs(f));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ADelayedMessageToTheTriggeringUserDoesNothingWhenTheUserIsGone()
    {
        using var f = new TeleportFixture();
        DelayedShowMessage(f);
        var replies = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(500);
        LeaveRoom(f);
        f.Advance(3000);
        f.Room.FlushWiredFlashes();

        Assert.Empty(Chats(replies));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void ADelayedMessageToTheTriggeringUserArrivesWhenTheUserStays()
    {
        using var f = new TeleportFixture();
        DelayedShowMessage(f);
        var replies = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(3500);
        f.Room.FlushWiredFlashes();

        Assert.Equal("hello", Assert.Single(Chats(replies)).Text);
    }

    [Fact]
    public void ADelayedChainIsDroppedWhenItsRoomUnloads()
    {
        using var f = new TeleportFixture();
        DelayedToggle(f);
        var before = f.Target.LegacyDataString;

        f.Fire(land: false);
        f.Advance(500);
        f.Room.GetWired().Cleanup();
        f.Advance(3000);

        Assert.Equal(before, f.Target.LegacyDataString);
    }

    private static RoomUser Replace(TeleportFixture f, int virtualId, int x, int y)
    {
        f.Room.GetWired().BeforeActorLeaves(f.User);
        RoomUsers(f.Room).TryRemove(f.User.VirtualId, out _);
        f.User.Dispose();
        var next = new RoomUser(f.User.HabboId, 0, virtualId, f.Room, f.Client, TestChatEmotions.Unused, f.HandRewards) { X = x, Y = y };
        Admit(f.Room, next);

        return next;
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void DelayedShowMessageNeverReachesAReplacementOrANewVisit(int virtualId)
    {
        using var f = new TeleportFixture();
        DelayedShowMessage(f);
        var replies = Capture(f.Client);

        f.Fire(land: false);
        f.Advance(500);
        Replace(f, virtualId, 2, 2);
        f.Advance(3000);
        f.Room.FlushWiredFlashes();

        Assert.Empty(Chats(replies));
        Assert.Empty(f.Errors);
    }

    private static void DelayTeleport(TeleportFixture f)
    {
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(f.Action, new() { IntParams = [0, 100, 0], SelectedItems = [1], Delay = 4 }, out var config, out var error), error);
        f.Action.ApplyConfiguration(config);
        Assert.Equal(4, f.Action.Configuration.Delay);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void DelayedTeleportNeverMovesAReplacementOrANewVisit(int virtualId)
    {
        using var f = new TeleportFixture();
        DelayTeleport(f);

        f.Fire(land: false);
        f.Advance(500);
        var next = Replace(f, virtualId, 2, 2);

        for (var i = 0; i < 24; i++) {
            f.Advance(250);
        }

        Assert.Equal(new System.Drawing.Point(2, 2), next.Coordinate);
        Assert.Equal(8, f.Habbo.Effects.CurrentEffect);
        Assert.Empty(f.Errors);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(8)]
    public void RunningTeleportStepsNeverMoveAReplacementOrANewVisit(int virtualId)
    {
        using var f = new TeleportFixture();
        f.Fire(land: false);
        Assert.Equal(4, f.User.CurrentEffect);
        f.Advance(500);
        var next = Replace(f, virtualId, 2, 2);

        for (var i = 0; i < 16; i++) {
            f.Advance(250);
        }

        Assert.Equal(new System.Drawing.Point(2, 2), next.Coordinate);
        Assert.Equal(8, next.CurrentEffect);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void Control_RunningTeleportMovesTheUserWhoStays()
    {
        using var f = new TeleportFixture();
        f.Fire(land: false);

        for (var i = 0; i < 16; i++) {
            f.Advance(250);
        }

        Assert.Equal(new System.Drawing.Point(1, 1), f.User.Coordinate);
    }

    [Fact]
    public void Control_DelayedTeleportMovesTheUserWhoStays()
    {
        using var f = new TeleportFixture();
        DelayTeleport(f);
        Assert.NotEqual(new System.Drawing.Point(1, 1), f.User.Coordinate);

        f.Fire(land: false);
        var trace = new List<string>();

        for (var i = 0; i < 24; i++) {
            f.Advance(250);
            trace.Add($"{(i + 1) * 250}:{f.User.CurrentEffect}:{f.User.Coordinate}");
        }

        Assert.True(new System.Drawing.Point(1, 1) == f.User.Coordinate, string.Join(" ", trace) + " errors=" + string.Join(";", f.Errors));
    }
}
