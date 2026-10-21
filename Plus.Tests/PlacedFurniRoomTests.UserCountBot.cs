using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms.AI.Speech;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: with the owner and one bot in the room, the user count conditions see one user.
    [Theory]
    [InlineData("wf_cnd_user_count_in", 1, 1, true)]
    [InlineData("wf_cnd_user_count_in", 2, 5, false)]
    [InlineData("wf_cnd_not_user_count", 1, 1, false)]
    [InlineData("wf_cnd_not_user_count", 2, 2, true)]
    public void ASpokenStackDoesNotCountABotInTheUserCountConditions(string name, int minimum, int maximum, bool shown)
    {
        var (_, actor) = PrepareSpeech(new SpeechClock());
        var speech = new List<RandomSpeech>();
        _room.GetRoomUserManager().DeployBot(new(1, 0, "generic", "stand", "Robbie", "", "hr-100", 2, 2, 0, 6, 0, 0, 0, 0, ref speech, "M", 0,
            7, false, 7, false, 0), null!);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        AddSpeechBox(302, name, new() { IntParams = [minimum, maximum, 0] }, 1);
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);

        Assert.Equal(shown, SpeechMessages(ServerPacketHeader.ChatComposer).Contains("A"));
    }
}
