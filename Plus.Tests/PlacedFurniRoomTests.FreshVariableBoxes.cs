using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (three runs, identical): a fresh Variable Age condition and a fresh Variable Changed trigger save
// as the editor opens them, with no variable. They reopen as saved and never act: the condition fails, and the trigger does not fire when a furni
// variable or a user variable is given or changed (a trigger on that variable does). Both cards also save on any variable id, in the form of
// Give Variable: a number is kept and other text is stored as "0" (an id of this hotel's own catalog is kept as it is).
public partial class PlacedFurniRoomTests
{
    private const uint FreshAge = 721, FreshChanged = 722;

    [Fact]
    public void AFreshVariableAgeConditionSavesAndReopensAsSaved()
    {
        var service = UseVariableRows();
        PlaceVariableBox(FreshAge, "wf_cnd_var_age_match");

        Assert.Equal("saved", SaveFreshAge(service, [1, 0, 0, 0, 0, 1, 0, -10], "0", "0"));

        var frame = ReopenFresh(FreshAge, ServerPacketHeader.WiredConditionConfigComposer);
        Assert.Equal([1, 0, 0, 0, 0, 1, 0, -10], frame.Ints);
        Assert.Equal(["0", "0"], frame.Variables);
        Assert.Equal([0, 200], frame.Furni);
        Assert.Equal([0, 200], frame.Users);
    }

    [Fact]
    public void AFreshVariableChangedTriggerSavesAndReopensAsSaved()
    {
        var service = UseVariableRows();
        PlaceVariableBox(FreshChanged, "wf_trg_var_changed");

        Assert.Equal("saved", SaveFreshChanged(service, "0"));

        var frame = ReopenFresh(FreshChanged, ServerPacketHeader.WiredTriggeRconfigComposer);
        Assert.Equal([1, 1, 0, 7, -1, 0], frame.Ints);
        Assert.Equal(["0"], frame.Variables);
        Assert.Empty(frame.Furni);
        Assert.Empty(frame.Users);
    }

    // Ids of the probe: a custom variable id, a number that names no variable, text, empty, and a negative number.
    [Theory]
    [InlineData("313917", "313917")]
    [InlineData("-99999", "-99999")]
    [InlineData("5", "5")]
    [InlineData("n", "0")]
    [InlineData("abc", "0")]
    [InlineData("", "0")]
    public void VariableAgeAndVariableChangedSaveAnyVariableId(string sent, string stored)
    {
        var service = UseVariableRows();
        PlaceVariableBox(FreshAge, "wf_cnd_var_age_match");
        PlaceVariableBox(FreshChanged, "wf_trg_var_changed");

        foreach (var target in new[] { 0, 1 }) {
            Assert.Equal("saved", SaveFreshAge(service, [target, 0, 0, 0, 0, 1, 0, -10], sent, "0"));
            Assert.Equal([stored, "0"], ReopenFresh(FreshAge, ServerPacketHeader.WiredConditionConfigComposer).Variables);
        }

        Assert.Equal("saved", SaveFreshChanged(service, sent));
        Assert.Equal([stored], ReopenFresh(FreshChanged, ServerPacketHeader.WiredTriggeRconfigComposer).Variables);
    }

    // The control stack runs on the same say; the stack with the fresh condition does not.
    [Fact]
    public void AFreshVariableAgeConditionFailsItsStack()
    {
        var (wired, actor) = PrepareSameTickVariable();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        AddSpeechBox(302, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "plain" }, 1);
        AddSpeechBox(311, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 3, 3);
        var age = WiredBox(FreshAge, "wf_cnd_var_age_match", 3, 3);
        age.Item.GetZ = 0.5;
        Assert.True(_room.GetWired().AddBox(age));
        Assert.Equal("saved", SaveFreshAge(service, [1, 0, 0, 0, 0, 1, 0, -10], "0", "0"));
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "aged" }, 1, 3, 3);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "go", false);
        wired.OnFastCycle();

        var messages = SpeechMessages(ServerPacketHeader.ChatComposer);
        Assert.Contains("plain", messages);
        Assert.DoesNotContain("aged", messages);
    }

    // A give and a change of user variable 993 fire the trigger on that variable and not the fresh one.
    [Fact]
    public void AFreshVariableChangedTriggerDoesNotFire()
    {
        var (wired, actor) = PrepareSameTickVariable();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vc1" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0], ["user:993", "n"], 1, 3, 3);
        AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], ["user:993"], 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "control fired" }, 1, 2, 2);
        var fresh = WiredBox(FreshChanged, "wf_trg_var_changed", 0, 3);
        Assert.True(_room.GetWired().AddBox(fresh));
        Assert.Equal("saved", SaveFreshChanged(service, "0"));
        AddSpeechBox(332, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "fresh fired" }, 1, 0, 3);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "vg1", false);
        wired.OnFastCycle();
        actor.OnChat(0, "vc1", false);
        wired.OnFastCycle();

        Assert.Equal(["vg1", "control fired", "vc1", "control fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    // The raw condition save of the editor's own frame: ints, no text, no picks, quantifier 0, the two sources, two variables, no second picks.
    private string SaveFreshAge(WiredConfigurationService service, int[] ints, string variable, string duration)
    {
        object[] body = [(int)FreshAge, ints.Length, .. ints.Cast<object>(), "", 0, 0, 2, 0, 200, 2, 0, 200, 2, variable, duration, 0];
        _client.Packets.Clear();
        new SaveWiredConditionConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    // The raw trigger save of the editor's own frame: six ints, no text, no picks, no sources, one variable, no second picks.
    private string SaveFreshChanged(WiredConfigurationService service, string variable)
    {
        object[] body = [(int)FreshChanged, 6, 1, 1, 0, 7, -1, 0, "", 0, 0, 0, 1, variable, 0];
        _client.Packets.Clear();
        new SaveWiredTriggerConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    // The raw WIRED_OPEN packet, then the ints, variables and sources of the frame that follows the picks, the sprite, the item id and the text.
    private (int[] Ints, string[] Variables, int[] Furni, int[] Users) ReopenFresh(uint id, uint header)
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)id)).GetAwaiter().GetResult();
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == header).Body };
        frame.ReadInt();

        for (var list = 0; list < 2; list++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                frame.ReadInt();
            }
        }

        frame.ReadInt();
        frame.ReadInt();
        frame.ReadString();
        int[] Ints() => [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];
        var ints = Ints();
        string[] variables = [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString())];

        return (ints, variables, Ints(), Ints());
    }
}
