using System.Collections.Immutable;
using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint SameTickVariableId = 993;

    // Observed on AIR WIN63-202610151745-318700621: a say whose stack gives a user variable fires the Variable Changed stack in the same
    // tick, and its message comes after the echo of the say.
    [Fact]
    public void VariableChangedStackRunsInTheTickOfTheSayAfterItsEcho()
    {
        var (wired, actor) = PrepareSameTickVariable();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], ["user:993"], 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "vc fired" }, 1, 2, 2);
        wired.FlushFlashes();
        _client.Packets.Clear();

        actor.OnChat(0, "vg1", false);

        Assert.Equal(["vg1", "vc fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
        wired.OnFastCycle();
        Assert.Equal(["vg1", "vc fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    // A Variable Changed stack that changes its own variable runs again in the same tick, but the depth limit still ends the chain.
    [Fact]
    public void VariableChangedStackThatChangesItsOwnVariableStopsAtTheDepthLimit()
    {
        var (wired, actor) = PrepareSameTickVariable();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], ["user:993"], 0, 2, 2);
        AddVariableBox(312, "wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0], ["user:993", "n"], 1, 2, 2);
        AddSpeechBox(313, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "loop" }, 2, 2, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "vg1", false);
        var sameTick = SpeechMessages(ServerPacketHeader.ChatComposer).Count(message => message == "loop");

        for (var cycle = 0; cycle < 50; cycle++) {
            wired.OnFastCycle();
        }

        var total = SpeechMessages(ServerPacketHeader.ChatComposer).Count(message => message == "loop");
        Assert.True(sameTick > 1, $"only {sameTick} Variable Changed runs in the tick of the say");
        Assert.Equal(sameTick, total);
        Assert.True(total <= new Plus.HabboHotel.Items.Wired.WiredEngineLimits().MaxDepth, $"{total} runs passed the depth limit");
    }

    private (Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor) PrepareSameTickVariable()
    {
        var prepared = PrepareSpeech(new SpeechClock());
        var module = new WiredVariableModule(RoomId, new SameTickVariableDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(prepared.Wired.Variables, module);

        return prepared;
    }

    private void AddVariableBox(uint id, string name, int[] owned, string[] variables, double height, int x, int y)
    {
        var box = WiredBox(id, name, x, y);
        box.Item.GetZ = height;
        WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = ImmutableArray.Create(owned),
            VariableIds = [.. variables]
        });
        Assert.True(_room.GetWired().AddBox(box));
    }

    private sealed class SameTickVariableDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id == SameTickVariableId
            ? new(id, RoomId, 7, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }
}
