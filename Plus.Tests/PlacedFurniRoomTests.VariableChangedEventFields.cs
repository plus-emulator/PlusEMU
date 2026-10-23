using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint ValuedEventVariableId = 995;
    private const uint ValuelessEventVariableId = 996;

    // A Give, also a Give with override of an existing value, reports change type 0 with no old value and no difference. A Change reports both, also when it assigns the same value.
    [Fact]
    public void VariableChangedEventFieldsOfAGiveHaveNoOldValueAndNoDifference()
    {
        var (wired, actor) = PrepareEventFieldVariables();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "give" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 11, 1, 0, -10, 0], [$"user:{ValuedEventVariableId}", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "assign" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_change_var_val", [1, 0, 0, 0, 11, -10, 0], [$"user:{ValuedEventVariableId}", "n"], 1, 3, 3);
        AddSpeechBox(331, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "subtract" }, 0, 0, 3);
        AddVariableBox(332, "wf_act_change_var_val", [1, 2, 0, 0, 3, -10, 0], [$"user:{ValuedEventVariableId}", "n"], 1, 0, 3);
        AddEventFieldStack(311, [1, 1, 0, 7, -1], ValuedEventVariableId, "ct=$(ct) ov=$(ov) nv=$(nv) df=$(df)", 2, 2);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "give", false);
        actor.OnChat(0, "give", false);
        actor.OnChat(0, "assign", false);
        actor.OnChat(0, "subtract", false);

        Assert.Equal(["ct=0 ov=null nv=11 df=null", "ct=0 ov=null nv=11 df=null", "ct=1 ov=11 nv=11 df=0", "ct=1 ov=11 nv=8 df=-3"],
            EventFieldMessages());
    }

    // A Give of a variable without a value reports no new value.
    [Fact]
    public void VariableChangedEventFieldsOfAVariableWithoutAValueHaveNoNewValue()
    {
        var (wired, actor) = PrepareEventFieldVariables();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "flag" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 0, 1, 0, -10, 0], [$"user:{ValuelessEventVariableId}", "n"], 1, 1, 1);
        AddEventFieldStack(311, [1, 1, 0, 7, -1], ValuelessEventVariableId, "ct=$(ct) ov=$(ov) nv=$(nv) df=$(df)", 2, 2);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "flag", false);

        Assert.Equal(["ct=0 ov=null nv=null df=null"], EventFieldMessages());
    }

    // A Remove reports change type 2 with no new value.
    [Fact]
    public void VariableChangedEventFieldsOfARemoveHaveNoNewValue()
    {
        var (wired, actor) = PrepareEventFieldVariables();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "give" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 9, 1, 0, -10, 0], [$"user:{ValuedEventVariableId}", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "remove" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_remove_var", [1], [$"user:{ValuedEventVariableId}"], 1, 3, 3);
        AddEventFieldStack(311, [0, 0, 1, 7, -1], ValuedEventVariableId, "ct=$(ct) nv=$(nv)", 2, 2);
        wired.FlushFlashes();
        actor.OnChat(0, "give", false);
        _client.Packets.Clear();

        actor.OnChat(0, "remove", false);

        Assert.Equal(["ct=2 nv=null"], EventFieldMessages());
    }

    private string[] EventFieldMessages() =>
        [.. SpeechMessages(ServerPacketHeader.ChatComposer).Where(message => message.StartsWith("ct=", StringComparison.Ordinal))];

    private void AddEventFieldStack(uint id, int[] trigger, uint variableId, string text, int x, int y)
    {
        AddVariableBox(id, "wf_trg_var_changed", trigger, [$"user:{variableId}"], 0, x, y);
        AddSpeechBox(id + 1, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = text }, 1, x, y);
        AddEventFieldOutput(id + 2, "ct", "-460", x, y);
        AddEventFieldOutput(id + 3, "ov", "-461", x, y);
        AddEventFieldOutput(id + 4, "nv", "-462", x, y);
        AddEventFieldOutput(id + 5, "df", "-463", x, y);
    }

    private void AddEventFieldOutput(uint id, string placeholder, string variable, int x, int y)
    {
        var box = WiredBox(id, "wf_xtra_text_output_variable", x, y);
        box.Item.GetZ = 2;
        WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [0, -20, 0],
            VariableIds = [variable],
            Text = placeholder
        });
        Assert.True(_room.GetWired().AddBox(box));
    }

    private (Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor) PrepareEventFieldVariables()
    {
        var prepared = PrepareSpeech(new SpeechClock());
        var module = new WiredVariableModule(RoomId, new EventFieldDirectory(), new MemoryWiredVariableStore(), TimeProvider.System,
            new RoomWiredBuiltinVariables(_room, prepared.Wired.ReadBuiltin));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(prepared.Wired.Variables, module);

        return prepared;
    }

    private sealed class EventFieldDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            ValuedEventVariableId => new(id, RoomId, 7, "canon", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            ValuelessEventVariableId => new(id, RoomId, 7, "flag", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, false),
            _ => null
        };
    }
}
