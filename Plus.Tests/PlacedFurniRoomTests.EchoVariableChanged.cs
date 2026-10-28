using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using System.Reflection;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint EchoSourceId = 993;
    private const uint EchoMirrorId = 994;

    // A Variable Changed stack on an Echo runs when the variable the Echo mirrors changes.
    [Fact]
    public void VariableChangedOnAnEchoRunsWhenItsSourceChanges()
    {
        var prepared = PrepareSpeech(new SpeechClock());
        var module = new WiredVariableModule(RoomId, new EchoVariableDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(prepared.Wired.Variables, module);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], ["user:994"], 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "echo fired" }, 1, 2, 2);
        prepared.Wired.FlushFlashes();
        _client.Packets.Clear();

        prepared.Actor.OnChat(0, "vg1", false);

        Assert.Equal(["vg1", "echo fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    private sealed class EchoVariableDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            EchoSourceId => new(id, RoomId, 7, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            EchoMirrorId => new(id, RoomId, 7, "mirror", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true,
                Link: new(RoomId, new(WiredVariableTarget.User, "custom:993"), false)),
            _ => null
        };
    }
}
