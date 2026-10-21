using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

// A Has Variable box saved with a variable of another domain than its target reads as having no variable. Observed on AIR
// WIN63-202610201032-107964540: the condition fails and the negated one passes, like a box with no variable.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_cnd_has_var", "pulse", 0, "user:5")]
    [InlineData("wf_cnd_neg_has_var", "A", 1, "ctx:5")]
    [InlineData("wf_cnd_has_var", "pulse", 1, "furni:5")]
    [InlineData("wf_cnd_neg_has_var", "A", 1, "furni:5")]
    [InlineData("wf_cnd_has_var", "pulse", -10, "ctx:5")]
    [InlineData("wf_cnd_neg_has_var", "A", -20, "room:5")]
    public void AVariableOfAnotherDomainThanTheTargetIsLoadedAndReadsAsNoVariable(string name, string expected, int target, string variable)
    {
        var native = HasVarNative(name) with { OwnedIntParams = [target], VariableIds = [variable] };
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        SetStore(wired, new(HasVarId, name, 2, JsonSerializer.Serialize(native)));
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var item = HasVarItem(name, 1, 1);
        item.GetZ = 1;
        Assert.NotNull(wired.LoadWiredBox(item));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([expected], SpeechMessages(Plus.Communication.Packets.Outgoing.ServerPacketHeader.ChatComposer));
    }
}
