using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.Polls;
using Xunit;

namespace Plus.Tests;

// Two Leaves Room stacks: one toggles tile C at once, one toggles tile D after 5 s. Observed on AIR WIN63-202610201032-107964540: users in
// the room when someone leaves get the box flash; a user who leaves and enters again gets no flash for its own leave, its room entry
// already shows tile C toggled, and the delayed toggle of D still reaches it.
public partial class PlacedFurniRoomTests
{
    private const uint LeaveTileC = 401, LeaveTileD = 402, ToggleNowTrigger = 411, ToggleNow = 412, ToggleLaterTrigger = 421, ToggleLater = 422;
    private static readonly uint[] LeaveBoxes = [ToggleNowTrigger, ToggleNow, ToggleLaterTrigger, ToggleLater];

    [Fact]
    public void AUserWhoStaysGetsTheFlashOfAVisitorLeaving()
    {
        var (_, _, _, visitorClient, clock) = PrepareTwoUsers();
        var (c, _) = PlaceLeaveStacks();

        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, false);
        RoomPasses(clock, 150);

        var seen = DataUpdates(_client).SelectMany(update => update).ToArray();
        Assert.All(LeaveBoxes, box => Assert.Contains(seen, entry => entry.Id == box));
        Assert.Contains((c.Id, "1"), seen);
    }

    [Fact]
    public void AUserWhoReentersGetsNoFlashForItsOwnLeaveButTheOthersDo()
    {
        var (_, _, _, visitorClient, clock) = PrepareTwoUsers();
        var (c, _) = PlaceLeaveStacks();

        LeaveAndEnterAgain();
        RoomPasses(clock, 150);

        Assert.DoesNotContain(DataUpdates(_client).SelectMany(update => update), entry => LeaveBoxes.Contains(entry.Id));
        var visitorSeen = DataUpdates(visitorClient).SelectMany(update => update).ToArray();
        Assert.All(LeaveBoxes, box => Assert.Contains(visitorSeen, entry => entry.Id == box));
        Assert.Contains((c.Id, "1"), visitorSeen);
        Assert.Contains((c.Id, "1"), DataUpdates(_client).SelectMany(update => update));
    }

    [Fact]
    public void AnOwnerAloneWhoReentersSeesTheTileToggledWithoutAFlash()
    {
        var clock = new SpeechClock();
        PrepareSpeech(clock);
        var (c, _) = PlaceLeaveStacks();

        LeaveAndEnterAgain();
        RoomPasses(clock, 150);

        var updates = DataUpdates(_client);
        Assert.DoesNotContain(updates.SelectMany(update => update), entry => LeaveBoxes.Contains(entry.Id));
        Assert.Equal("1", c.LegacyDataString);
        Assert.Contains((c.Id, "1"), updates.SelectMany(update => update));
    }

    [Fact]
    public void TheDelayedToggleOfALeaveReachesTheUserWhoReentered()
    {
        var clock = new SpeechClock();
        PrepareSpeech(clock);
        var (_, d) = PlaceLeaveStacks();

        LeaveAndEnterAgain();
        RoomPasses(clock, 6000);

        Assert.Equal("1", d.LegacyDataString);
        Assert.Contains((d.Id, "1"), DataUpdates(_client).SelectMany(update => update));
    }

    private (Item C, Item D) PlaceLeaveStacks()
    {
        var c = ToggleAt(LeaveTileC, 1, 3);
        var d = ToggleAt(LeaveTileD, 3, 3);
        LeaveStack(ToggleNowTrigger, 2, c, 0);
        LeaveStack(ToggleLaterTrigger, 3, d, 10);
        _room.GetWired().FlushFlashes();
        _client.Packets.Clear();

        return (c, d);
    }

    private void LeaveStack(uint id, int x, Item target, int delay)
    {
        var trigger = WiredBox(id, "wf_trg_leave_room", x, 0);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_leave_room"));
        Assert.True(_room.GetWired().AddBox(trigger));
        var toggle = WiredBox(id + 1, "wf_act_toggle_state", x, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(toggle, new() { IntParams = [0, 100], SelectedItems = [target.Id], Delay = delay },
            out var config, out var error), error);
        toggle.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(toggle));
    }

    // A room reload: the client opens the room again (the server takes the user out of it) and asks for the entry data.
    private void LeaveAndEnterAgain()
    {
        _room.GetRoomUserManager().RemoveUserFromRoom(_client, false);
        _client.GetHabbo().CurrentRoom = _room;
        _client.GetHabbo().HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        _client.Packets.Clear();
        var quests = Proxy<IQuestManager>((method, _) => throw new InvalidOperationException(method));
        var polls = Proxy<IRoomPollService>((method, _) => method == nameof(IRoomPollService.Offer) ? null : throw new InvalidOperationException(method));
        new GetRoomEntryDataEvent(new RoomEntryService(quests, TimeProvider.System, polls, EntryWordQuiz))
            .Parse(_client, new FlashIncomingPacket { Buffer = Array.Empty<byte>() }).GetAwaiter().GetResult();
        Assert.NotNull(_room.GetRoomUserManager().GetRoomUserByHabbo(7));
    }

    // The room manager's cadence: a Wired pass every 50 ms and a full room pass on every 500 ms step.
    private void RoomPasses(SpeechClock clock, int milliseconds)
    {
        for (var passed = 50; passed <= milliseconds; passed += 50) {
            clock.Now = clock.Now.AddMilliseconds(50);

            if (passed % 500 == 0) {
                _room.ProcessRoom();
            }
            else {
                _room.ProcessWiredOnly();
            }
        }
    }

    // Each ObjectsDataUpdate as (item id, legacy state) pairs.
    private static (uint Id, string State)[][] DataUpdates(TestClient client) => [.. client.Packets
        .Where(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer)
        .Select(packet =>
        {
            var input = new FlashIncomingPacket { Buffer = packet.Body };

            return Enumerable.Range(0, input.ReadInt()).Select(_ =>
            {
                var id = (uint)input.ReadInt();
                Assert.Equal(0, input.ReadInt());

                return (id, input.ReadString());
            }).ToArray();
        })];
}
