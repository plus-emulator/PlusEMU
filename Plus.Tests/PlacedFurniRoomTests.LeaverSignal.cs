using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The stack that a Leaves Room stack reaches with Call Stack or Send Signal reads the leaver's variables like the Leaves Room stack
/// itself: only in its effects without a delay, and it never moves or messages the leaver.
/// </summary>
public partial class PlacedFurniRoomTests
{
    // The leaver has pu_a = 7. The Leaves Room stack reaches a stack that adds the triggering user's pu_a to pg_a. The stayer then reads pg_a.
    [Theory]
    [InlineData(false, 0, 0, "g=7")]
    [InlineData(true, 0, 0, "g=7")]
    [InlineData(false, 2, 0, "g=0")]
    [InlineData(false, 0, 2, "g=0")]
    [InlineData(true, 2, 0, "g=0")]
    [InlineData(true, 0, 2, "g=0")]
    public async Task AStackReachedFromALeaveStackReadsTheLeaversVariableOnlyAtOnce(bool call, int sendDelay, int receiveDelay, string expected)
    {
        var (wired, owner, clock, visitor, visitorClient) = await LeaveReachRoom(call, sendDelay, receiveDelay, userEffects: false);

        DpFire(wired, visitor, clock, 1);
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 3000);

        Assert.Contains(expected, DpFire(wired, owner, clock, 1));
    }

    // The reached stack also teleports and messages the triggering user at once. The leaver is gone, so none of it reaches the leaver, while the variable copy still runs.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AStackReachedFromALeaveStackDoesNotReachTheLeaver(bool call)
    {
        var (wired, owner, clock, visitor, visitorClient) = await LeaveReachRoom(call, 0, 0, userEffects: true);
        var start = (visitor.X, visitor.Y);

        DpFire(wired, visitor, clock, 1);
        visitorClient.Packets.Clear();
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 3000);

        Assert.Equal(start, (visitor.X, visitor.Y));
        Assert.DoesNotContain(visitorClient.Packets, packet => packet.Header is ServerPacketHeader.WhisperComposer or ServerPacketHeader.ChatComposer);
        Assert.Contains("g=7", DpFire(wired, owner, clock, 1));
    }

    // A signal from a stack that another user triggers in the same moment reads that user's variable, not the leaver's.
    [Fact]
    public async Task ASignalFromAnotherStackDoesNotReadTheLeaver()
    {
        var (wired, owner, clock, visitor, visitorClient) = await LeaveReachRoom(false, 0, 0, userEffects: false, leaveSends: false);

        DpFire(wired, visitor, clock, 1);
        DpFire(wired, owner, clock, 1);
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        owner.OnChat(0, "other", false);
        Advance(wired, clock, 3000);

        Assert.Contains("g=7", DpFire(wired, owner, clock, 1));
    }

    private async Task<(WiredComponent Wired, RoomUser Owner, SpeechClock Clock, RoomUser Visitor, TestClient VisitorClient)> LeaveReachRoom(
        bool call, int sendDelay, int receiveDelay, bool userEffects, bool leaveSends = true)
    {
        var (wired, owner, clock) = await DelayedPointerRoom();
        var visitor = AddLeaver(out var visitorClient);
        var antenna = Furni(SignalAntenna, InteractionType.None, WiredBoxType.None);
        antenna.Definition.InteractionName = "antenna";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, antenna, 3, 3, 0, true, false, false));

        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpShow("g=$(g)", 0);
        await DpOutput("g", DpGlobal, "room:" + DpGa);

        _dpTile = (3, 1);
        _dpZ = 0;
        var reached = _dpNext;

        if (!call) {
            AddSpeechBox(_dpNext++, "wf_trg_recv_signal", new() { IntParams = [0, 100], SelectedItems = [SignalAntenna] }, _dpZ++, 3, 1);
            reached = _dpNext;
        }

        await DpEffect("wf_act_change_var_val", [DpGlobal, 1, 1, 0, 0, DpUser, 0], ["room:" + DpGa, "user:" + DpUa],
            furni: [0, 0], users: [0, 0], delay: receiveDelay);

        if (userEffects) {
            await DpEffect("wf_act_teleport_to", [0, 100, 0], [], furni: [100], users: [0], picks: [DpHolder], delay: receiveDelay);
            await DpEffect("wf_act_show_message", [0, 202, -1], [], text: "late", furni: [], users: [0], delay: receiveDelay);
        }

        _dpTile = (2, 1);
        _dpZ = 0;
        AddLeaveTrigger(2, 1);

        if (leaveSends) {
            if (call) {
                await DpEffect("wf_act_call_stacks", [], [], furni: [100], users: [], picks: [reached], delay: sendDelay);
            }
            else {
                await DpEffect("wf_act_send_signal", [0, 0], [], furni: [100, 100], users: [0], picks: [SignalAntenna], delay: sendDelay);
            }
        }
        else {
            _dpTile = (3, 2);
            _dpZ = 0;
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "other" }, _dpZ++, 3, 2);
            await DpEffect("wf_act_send_signal", [0, 0], [], furni: [100, 100], users: [0], picks: [SignalAntenna]);
        }

        return (wired, owner, clock, visitor, visitorClient);
    }
}
