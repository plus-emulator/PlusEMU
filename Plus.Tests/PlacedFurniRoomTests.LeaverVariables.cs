using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A Leaves Room stack runs in the same millisecond as the removal and can still read and change the leaver's user variables. A
/// delayed part of that stack reads nothing, and nothing comes back when the user returns.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const int LeaverId = 8;

    // A Leaves Room stack copies the leaver's pu_a into the global pg_a, at once or after a delay. The stayer then reads pg_a.
    [Theory]
    [InlineData(0, "g=7")]
    [InlineData(4, "g=0")]
    [InlineData(-1, "g=7")]
    [InlineData(-2, "g=33")]
    public async Task ALeaveStackReadsTheLeaversVariableOnlyAtOnce(int delay, string expected)
    {
        var control = delay == -1;
        var literal = delay == -2;
        delay = Math.Max(delay, 0);
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpShow("g=$(g)", 0);
        await DpOutput("g", DpGlobal, "room:" + DpGa);

        _dpTile = (2, 1);
        _dpZ = 0;

        if (control) {
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "copy" }, _dpZ++, 2, 1);
        }
        else {
            AddLeaveTrigger(2, 1);
        }

        if (literal) {
            await DpEffect("wf_act_change_var_val", [DpGlobal, 0, 0, 0, 33, -10, 0], ["room:" + DpGa, "n"], furni: [0, 0], users: [0, 0]);
        }
        else {
            await DpEffect("wf_act_change_var_val", [DpGlobal, 0, 1, 0, 0, DpUser, 0], ["room:" + DpGa, "user:" + DpUa],
                furni: [0, 0], users: [0, 0], delay: delay);
        }

        DpFire(wired, visitor, clock, 1);

        if (control) {
            visitor.OnChat(0, "copy", false);
            Advance(wired, clock, 1000);
        }

        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        DpFire(wired, owner, clock, 1);
        Advance(wired, clock, 3000);

        Assert.Contains(expected, DpFire(wired, owner, clock, 1));
    }

    // The first effect adds 5 to the leaver's pu_a and the second copies it into pg_a, both at once.
    [Fact]
    public async Task ALeaveStackReadsAValueItChangedForTheLeaverAtOnce()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpShow("g=$(g)", 0);
        await DpOutput("g", DpGlobal, "room:" + DpGa);

        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        PlaceSavedBox(_dpNext++, "wf_xtra_exec_in_order", 2, 1, _dpZ++);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 5, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);
        await DpEffect("wf_act_change_var_val", [DpGlobal, 0, 1, 0, 0, DpUser, 0], ["room:" + DpGa, "user:" + DpUa], furni: [0, 0], users: [0, 0]);

        DpFire(wired, visitor, clock, 1);
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        DpFire(wired, owner, clock, 1);
        Advance(wired, clock, 3000);

        Assert.Contains("g=12", DpFire(wired, owner, clock, 1));
    }

    // The leaver gets pu_a = 7 before leaving. After the return, the same user reads no value.
    [Fact]
    public async Task ALeaverGetsNoVariableBackOnReturn()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpShow("a=$(a)", 0);
        await DpOutput("a", DpUser, "user:" + DpUa);
        await GiveOnSetWord();
        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 5, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0], delay: 2);

        Say(wired, visitor, clock, "set");
        Assert.Equal(["a=7"], VisitorMessages(wired, visitor, visitorClient, clock));
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 3000);

        visitorClient.GetHabbo().CurrentRoom = _room;
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(visitorClient));
        var returned = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(LeaverId));

        Assert.Equal(["a=null"], VisitorMessages(wired, returned, visitorClient, clock));
    }

    // A Teleport and a Show Message that wait in the Leaves Room stack find nobody once the user is gone. The chain itself keeps running.
    [Fact]
    public async Task ADelayedTeleportAndMessageInALeaveStackDoNotReachTheLeaver()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);
        var start = (visitor.X, visitor.Y);

        await DpShow("g=$(g)", 0);
        await DpOutput("g", DpGlobal, "room:" + DpGa);
        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        await DpEffect("wf_act_teleport_to", [0, 100, 0], [], furni: [100], users: [0], picks: [DpHolder], delay: 2);
        await DpEffect("wf_act_show_message", [0, 202, -1], [], text: "late", furni: [], users: [0], delay: 3);
        await DpEffect("wf_act_change_var_val", [DpGlobal, 0, 0, 0, 33, -10, 0], ["room:" + DpGa, "n"], furni: [0, 0], users: [0, 0], delay: 4);

        visitorClient.Packets.Clear();
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 4000);

        Assert.Equal(start, (visitor.X, visitor.Y));
        Assert.DoesNotContain(visitorClient.Packets, packet => packet.Header is ServerPacketHeader.WhisperComposer or ServerPacketHeader.ChatComposer);
        Assert.Contains("g=33", DpFire(wired, owner, clock, 1));
    }

    // The leave stack changes the leaver's pu_a. The stayer's pu_a stays as it was.
    [Fact]
    public async Task ALeaveStackChangesOnlyTheLeaversVariable()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpShow("a=$(a)", 0);
        await DpOutput("a", DpUser, "user:" + DpUa);
        await GiveOnSetWord();
        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 5, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);

        Say(wired, owner, clock, "set");
        Say(wired, visitor, clock, "set");
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 3000);

        Assert.Equal(["a=7"], DpFire(wired, owner, clock, 1));
    }

    // The user leaves and comes back before the room runs the leave stack. The old value must not come back.
    [Fact]
    public async Task AUserWhoReturnsBeforeTheLeaveRunsGetsNoOldValue()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpShow("a=$(a)", 0);
        await DpOutput("a", DpUser, "user:" + DpUa);
        await GiveOnSetWord();
        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 5, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);

        Say(wired, visitor, clock, "set");
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        EnterVisitor(visitorClient);
        var returned = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(LeaverId));
        Advance(wired, clock, 3000);

        Assert.Equal(["a=null"], VisitorMessages(wired, returned, visitorClient, clock));
    }

    // The user comes back and an Enters Room stack gives pu_a = 3. That new value must survive the old leave.
    [Fact]
    public async Task AUserWhoReturnsBeforeTheLeaveRunsKeepsTheNewValue()
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);

        await DpShow("a=$(a)", 0);
        await DpOutput("a", DpUser, "user:" + DpUa);
        await GiveOnSetWord();
        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 5, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);
        _dpTile = (2, 2);
        _dpZ = 0;
        AddTriggerAt("wf_trg_enter_room", 2, 2);
        await DpGive(DpUser, "user:" + DpUa, 3);

        Say(wired, visitor, clock, "set");
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        EnterVisitor(visitorClient);
        var returned = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(LeaverId));
        Advance(wired, clock, 3000);

        Assert.Equal(["a=3"], VisitorMessages(wired, returned, visitorClient, clock));
    }

    private void AddTriggerAt(string name, int x, int y)
    {
        var trigger = WiredBox(_dpNext++, name, x, y);
        trigger.Item.GetZ = _dpZ++;
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults(name));
        Assert.True(_room.GetWired().AddBox(trigger));
    }

    private void EnterVisitor(TestClient client)
    {
        client.GetHabbo().CurrentRoom = _room;
        client.GetHabbo().HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        var quests = Proxy<Plus.HabboHotel.Quests.IQuestManager>((method, _) => throw new InvalidOperationException(method));
        var polls = Proxy<Plus.HabboHotel.Rooms.Polls.IRoomPollService>((method, _) => method == nameof(Plus.HabboHotel.Rooms.Polls.IRoomPollService.Offer) ? null : throw new InvalidOperationException(method));
        new RoomEntryService(quests, TimeProvider.System, polls, EntryWordQuiz).Enter(client);
    }

    private RoomUser AddLeaver(out TestClient client)
    {
        client = new();
        client.SetHabbo(new Habbo { Id = LeaverId, Username = "guest", CurrentRoom = _room, Access = UserAccess.Empty });
        client.GetHabbo().IgnoresComponent = new([]);
        typeof(RoomUserManager).GetField("_rewards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_room.GetRoomUserManager(), new TestRewardProgress());
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(client));

        return Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(LeaverId));
    }

    // Whoever says "set" gets pu_a = 7.
    private async Task GiveOnSetWord()
    {
        _dpTile = (3, 1);
        _dpZ = 0;
        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "set" }, _dpZ++, 3, 1);
        await DpGive(DpUser, "user:" + DpUa, 7);
    }

    private void Say(WiredComponent wired, RoomUser speaker, SpeechClock clock, string word)
    {
        speaker.OnChat(0, word, false);
        Advance(wired, clock, 1000);
    }

    private void AddLeaveTrigger(int x, int y)
    {
        var trigger = WiredBox(_dpNext++, "wf_trg_leave_room", x, y);
        trigger.Item.GetZ = _dpZ++;
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_leave_room"));
        Assert.True(_room.GetWired().AddBox(trigger));
    }

    private string[] VisitorMessages(WiredComponent wired, RoomUser visitor, TestClient client, SpeechClock clock)
    {
        wired.FlushFlashes();
        client.Packets.Clear();
        visitor.OnChat(0, "go", false);
        Advance(wired, clock, 1000);

        return [.. client.Packets.Where(packet => packet.Header == ServerPacketHeader.WhisperComposer).Select(packet =>
        {
            var input = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = packet.Body };
            input.ReadInt();

            return input.ReadString();
        })];
    }
}
