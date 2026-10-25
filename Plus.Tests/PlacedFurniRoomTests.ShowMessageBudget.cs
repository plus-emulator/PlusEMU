using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private static int Lines(TestClient client, uint header, string text) => ChatTexts(client, header).Count(line => line == text);

    // Each user gets 10 lines in 5 s of their own, whatever the other user receives.
    [Fact]
    public void ShowMessageBudgetBelongsToTheReceivingUser()
    {
        var (wired, owner, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 0, 34, -1], Text = "line" }, 1);

        for (var i = 0; i < 12; i++) {
            owner.OnChat(0, "go", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        for (var i = 0; i < 12; i++) {
            visitor.OnChat(0, "go", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        Advance(wired, clock, 1000);
        Assert.Equal((10, 10), (Lines(_client, ServerPacketHeader.WhisperComposer, "line"), Lines(visitorClient, ServerPacketHeader.WhisperComposer, "line")));
    }

    // Lines from another user's stack and from the user's own stack come out of the same budget.
    [Fact]
    public void ShowMessageLinesFromAnotherUsersStackShareTheReceiversBudget()
    {
        var (wired, owner, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "send" }, 0);
        AddSpeechBox(302, "wf_slc_users_byname", new() { IntParams = [0, 0], Text = "guest" }, 1);
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [200, 0, 34, -1], Text = "from owner" }, 2);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "own" }, 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 0, 34, -1], Text = "from self" }, 1, 2, 2);

        for (var i = 0; i < 10; i++) {
            owner.OnChat(0, "own", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        for (var i = 0; i < 6; i++) {
            owner.OnChat(0, "send", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        for (var i = 0; i < 6; i++) {
            visitor.OnChat(0, "own", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        Advance(wired, clock, 1000);
        var received = ChatTexts(visitorClient, ServerPacketHeader.WhisperComposer);
        Assert.Equal((6, 4), (received.Count(line => line == "from owner"), received.Count(line => line == "from self")));
    }

    // The sliding 30 s window holds 40 lines per user, one second apart so the 5 s window never applies.
    [Fact]
    public void ShowMessageAllowsFortyLinesPerUserInThirtySeconds()
    {
        var (wired, owner, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 0, 34, -1], Text = "line" }, 1);

        for (var i = 0; i < 30; i++) {
            foreach (var user in new[] { owner, visitor }) {
                for (var k = 0; k < 2; k++) {
                    user.OnChat(0, "go", false);
                }
            }

            Advance(wired, clock, 1000);
        }

        Assert.Equal((40, 40), (Lines(_client, ServerPacketHeader.WhisperComposer, "line"), Lines(visitorClient, ServerPacketHeader.WhisperComposer, "line")));
    }

    // A line shown to the whole room counts against each receiver's budget; a receiver with no budget left does not get it.
    [Fact]
    public void ShowMessageToTheRoomSkipsReceiversWithoutBudget()
    {
        var (wired, owner, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "priv" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 0, 34, -1], Text = "private" }, 1);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "all" }, 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "everyone" }, 1, 2, 2);

        for (var i = 0; i < 10; i++) {
            visitor.OnChat(0, "priv", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        Advance(wired, clock, 500);

        owner.OnChat(0, "all", false);
        Advance(wired, clock, 1000);
        Assert.Equal((1, 0), (Lines(_client, ServerPacketHeader.ChatComposer, "everyone"), Lines(visitorClient, ServerPacketHeader.ChatComposer, "everyone")));
    }

    // The budget ends when the user leaves: a user who comes back starts with a full budget.
    [Fact]
    public void ShowMessageBudgetEndsWhenTheUserLeaves()
    {
        var (wired, _, visitor, visitorClient, clock) = PrepareTwoUsers();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 0, 34, -1], Text = "line" }, 1);

        for (var i = 0; i < 10; i++) {
            visitor.OnChat(0, "go", false);
            clock.Now = clock.Now.AddMilliseconds(100);
            wired.OnFastCycle();
        }

        Advance(wired, clock, 500);
        _room.GetRoomUserManager().RemoveUserFromRoom(visitorClient, true, false);
        visitorClient.Packets.Clear();
        visitorClient.GetHabbo().CurrentRoom = _room;
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(visitorClient));
        var returned = Assert.IsType<Plus.HabboHotel.Rooms.RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(8));
        visitorClient.Packets.Clear();

        returned.OnChat(0, "go", false);
        Advance(wired, clock, 1000);
        Assert.Equal(1, Lines(visitorClient, ServerPacketHeader.WhisperComposer, "line"));
    }
}
