using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Two real users in the real room: the owner (7) and a visitor (8). Observed on AIR WIN63-202610201032-107964540.
public partial class PlacedFurniRoomTests
{
    private (WiredComponent Wired, RoomUser Owner, RoomUser Visitor, TestClient VisitorClient, SpeechClock Clock) PrepareTwoUsers()
    {
        var clock = new SpeechClock();
        var (wired, owner) = PrepareSpeech(clock);
        var visitorClient = new TestClient();
        visitorClient.SetHabbo(new Habbo { Id = 8, Username = "guest", CurrentRoom = _room, Access = UserAccess.Empty });
        visitorClient.GetHabbo().IgnoresComponent = new([]);
        typeof(RoomUserManager).GetField("_rewards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_room.GetRoomUserManager(), new TestRewardProgress());
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(visitorClient));
        var visitor = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(8));
        _client.Packets.Clear();
        visitorClient.Packets.Clear();

        return (wired, owner, visitor, visitorClient, clock);
    }

    // Room ticks on every 500 ms step, then one fast pass, as the room manager runs them.
    private static void Advance(WiredComponent wired, SpeechClock clock, int milliseconds)
    {
        for (var passed = 0; passed < milliseconds; passed += 500) {
            clock.Now = clock.Now.AddMilliseconds(500);
            wired.OnRoomTick();
            wired.OnFastCycle();
        }
    }

    private void AddTrigger(uint id, string name, double height = 0)
    {
        var trigger = WiredBox(id, name, 1, 1);
        trigger.Item.GetZ = height;
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults(name));
        Assert.True(_room.GetWired().AddBox(trigger));
    }

    private static string[] ChatTexts(TestClient client, uint header) => client.Packets.Where(packet => packet.Header == header)
        .Select(packet => { var input = new FlashIncomingPacket { Buffer = packet.Body }; input.ReadInt(); return input.ReadString(); }).ToArray();

    // The delayed Show Message of a Leaves Room stack finds its user gone and sends nothing.
    [Fact]
    public void ADelayedMessageOfALeaveRoomStackIsNotDeliveredToTheUserWhoLeft()
    {
        var (wired, _, _, visitorClient, clock) = PrepareTwoUsers();
        AddTrigger(301, "wf_trg_leave_room");
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "bye", Delay = 2 }, 1);

        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        Advance(wired, clock, 2500);
        Assert.Contains(_client.Sent, id => id is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.ObjectUpdateComposer);

        Assert.DoesNotContain("bye", ChatTexts(visitorClient, ServerPacketHeader.ChatComposer));
        Assert.DoesNotContain("bye", ChatTexts(visitorClient, ServerPacketHeader.WhisperComposer));
        Assert.DoesNotContain("bye", ChatTexts(_client, ServerPacketHeader.ChatComposer));
        Assert.DoesNotContain("bye", ChatTexts(_client, ServerPacketHeader.WhisperComposer));
    }

    // The same stack shows its delayed message to a user who is still in the room.
    [Fact]
    public void ADelayedMessageIsDeliveredToAUserWhoStays()
    {
        var (wired, _, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "hello", Delay = 2 }, 1);

        visitor.OnChat(0, "pulse", false);
        Advance(wired, clock, 1500);

        Assert.Contains("hello", ChatTexts(visitorClient, ServerPacketHeader.ChatComposer));
        Assert.Contains("hello", ChatTexts(_client, ServerPacketHeader.ChatComposer));
    }

    // The user count conditions count the two real users in the room.
    [Theory]
    [InlineData("wf_cnd_user_count_in", 2, 2, true)]
    [InlineData("wf_cnd_user_count_in", 1, 1, false)]
    [InlineData("wf_cnd_user_count_in", 3, 125, false)]
    [InlineData("wf_cnd_not_user_count", 2, 2, false)]
    [InlineData("wf_cnd_not_user_count", 1, 1, true)]
    public void TheUserCountConditionsCountTwoRealUsers(string name, int minimum, int maximum, bool shown)
    {
        var (_, _, visitor, _, _) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        AddSpeechBox(302, name, new() { IntParams = [minimum, maximum, 0] }, 1);
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 2);
        _client.Packets.Clear();

        visitor.OnChat(0, "pulse", false);

        Assert.Equal(shown, SpeechMessages(ServerPacketHeader.ChatComposer).Contains("A"));
    }

    // An Enter Room stack runs for a visitor who enters, and its message shows to the owner who is already inside.
    [Fact]
    public void EnterRoomFiresForAVisitorWhoEnters()
    {
        var clock = new SpeechClock();
        var (wired, _) = PrepareSpeech(clock);
        AddTrigger(301, "wf_trg_enter_room");
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "welcome" }, 1);
        var visitorClient = new TestClient();
        visitorClient.SetHabbo(new Habbo { Id = 8, Username = "guest", CurrentRoom = _room, Access = UserAccess.Empty });
        visitorClient.GetHabbo().IgnoresComponent = new([]);
        visitorClient.GetHabbo().HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        typeof(RoomUserManager).GetField("_rewards", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_room.GetRoomUserManager(), new TestRewardProgress());
        _client.Packets.Clear();
        var quests = Proxy<Plus.HabboHotel.Quests.IQuestManager>((method, _) => throw new InvalidOperationException(method));
        var polls = Proxy<Plus.HabboHotel.Rooms.Polls.IRoomPollService>((method, _) => method == nameof(Plus.HabboHotel.Rooms.Polls.IRoomPollService.Offer) ? null : throw new InvalidOperationException(method));

        new RoomEntryService(quests, TimeProvider.System, polls, EntryWordQuiz).Enter(visitorClient);
        Advance(wired, clock, 1000);

        Assert.Contains("welcome", ChatTexts(_client, ServerPacketHeader.ChatComposer));
        Assert.Contains("welcome", ChatTexts(visitorClient, ServerPacketHeader.ChatComposer));
    }
}
