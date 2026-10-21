using System.Collections.Concurrent;
using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: a say whose stack changes the value of a user variable fires the Variable Changed stack
    // in the tick of the say, after the echo, like a Give Variable does. The flashes of the say stack and of the fired stack are one batch.
    [Fact]
    public void VariableChangedStackRunsInTheTickOfASayThatChangesTheValue()
    {
        var (wired, actor) = PrepareSameTickVariable();
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, 1, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vc1" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0], ["user:993", "n"], 1, 3, 3);
        AddVariableBox(311, "wf_trg_var_changed", [1, 1, 1, 7, -1], ["user:993"], 0, 2, 2);
        AddSpeechBox(312, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "vc fired" }, 1, 2, 2);
        wired.FlushFlashes();
        actor.OnChat(0, "vg1", false);
        wired.OnFastCycle();
        _client.Packets.Clear();

        actor.OnChat(0, "vc1", false);

        Assert.Equal(["vc1", "vc fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
        wired.OnFastCycle();
        Assert.Equal(["vc1", "vc fired"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    // Observed on AIR WIN63-202610201032-107964540: a change of a parent variable fires the Variable Changed stacks of the parent and of
    // its Time Utilities parts in the tick of the say. A part whose value did not change fires as "unchanged".
    [Fact]
    public void VariableChangedStacksOfTimePartsRunInTheTickOfASayThatChangesTheParent()
    {
        var (wired, actor) = PrepareTimePartVariable(out var year, out var millisecond);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 1000, 1, 0, -10, 0], [$"user:{TimePartVariableId}", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vc1" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0], [$"user:{TimePartVariableId}", "n"], 1, 3, 3);
        AddPartTrigger(331, $"user:{year}", "year changed", 0, 1);
        AddPartTrigger(341, $"user:{millisecond}", "ms changed", 0, 2);
        AddPartTrigger(351, $"user:{TimePartVariableId}", "parent changed", 0, 3);
        wired.FlushFlashes();
        actor.OnChat(0, "vg1", false);
        wired.OnFastCycle();
        _client.Packets.Clear();

        actor.OnChat(0, "vc1", false);

        var inTick = SpeechMessages(ServerPacketHeader.ChatComposer);
        Assert.Equal("vc1", inTick[0]);
        Assert.Equal(["ms changed", "parent changed", "year changed"], inTick[1..].Order().ToArray());
    }

    // Observed on AIR WIN63-202610201032-107964540: adding to the year part changes the parent. The year part fires as "increased" and the
    // milliseconds part as "unchanged", in the tick of the say.
    [Fact]
    public void VariableChangedStacksOfTimePartsRunInTheTickOfASayThatChangesAPart()
    {
        var (wired, actor) = PrepareTimePartVariable(out var year, out var millisecond);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vg1" }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 1000, 1, 0, -10, 0], [$"user:{TimePartVariableId}", "n"], 1, 1, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "vc2" }, 0, 3, 3);
        AddVariableBox(322, "wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0], [$"user:{year}", "n"], 1, 3, 3);
        AddPartTrigger(331, $"user:{year}", "year up", 0, 1, increased: true);
        AddPartTrigger(341, $"user:{millisecond}", "ms same", 0, 2, unchanged: true);
        wired.FlushFlashes();
        actor.OnChat(0, "vg1", false);
        wired.OnFastCycle();
        _client.Packets.Clear();

        actor.OnChat(0, "vc2", false);

        var inTick = SpeechMessages(ServerPacketHeader.ChatComposer);
        Assert.Equal("vc2", inTick[0]);
        Assert.Equal(["ms same", "year up"], inTick[1..].Order().ToArray());
    }

    private const uint TimePartVariableId = 20;

    private void AddPartTrigger(uint id, string variable, string text, int x, int y, bool increased = true, bool unchanged = true)
    {
        var mask = (increased ? 1 : 0) | (unchanged ? 4 : 0);
        AddVariableBox(id, "wf_trg_var_changed", [1, 1, 1, mask, -1], [variable], 0, x, y);
        AddSpeechBox(id + 1, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = text }, 1, x, y);
    }

    private (Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, RoomUser Actor) PrepareTimePartVariable(out uint year, out uint millisecond)
    {
        var prepared = PrepareSpeech(new SpeechClock());
        var variables = prepared.Wired.Variables;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var floor = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", Private)!.GetValue(_room.GetRoomItemHandler())!;
        var resolve = typeof(WiredRoomVariables).GetMethod("ResolveDerived", Private)!.CreateDelegate<Func<WiredVariableReference, WiredVariableDerivation?>>(variables);
        var module = new WiredVariableModule(RoomId, new TimePartDirectory(), new MemoryWiredVariableStore(), TimeProvider.System, null, resolve);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", Private)!.SetValue(variables, module);
        var baseItem = new Item { Id = TimePartVariableId, OwnerId = 7, RoomId = RoomId, Definition = new() { Id = TimePartVariableId, InteractionName = "wf_var_user" } };
        floor[baseItem.Id] = baseItem;
        var definition = Assert.IsType<WiredVariableDefinitionBox>(variables.CreateBox(baseItem));
        WiredNativeTestSupport.InstallLegacyVariableDraft(definition, new() { IntParams = [1, 0], Text = "score" });
        variables.ConfigurationLoaded(definition);
        var timeItem = new Item { Id = 21, OwnerId = 7, RoomId = RoomId, Definition = new() { Id = 21, InteractionName = "wf_xtra_var_time_util" } };
        floor[timeItem.Id] = timeItem;
        var time = Assert.IsType<WiredVariableMetadataBox>(variables.CreateBox(timeItem));
        WiredNativeTestSupport.InstallLegacyVariableDraft(time, new() { IntParams = [(1 << 1) | (1 << 10), 0] });
        variables.ConfigurationLoaded(time);
        year = WiredRoomVariables.SyntheticId(WiredVariableTarget.User, TimePartVariableId, 10, true)!.Value;
        millisecond = WiredRoomVariables.SyntheticId(WiredVariableTarget.User, TimePartVariableId, 1, true)!.Value;

        return prepared;
    }

    private sealed class TimePartDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id == TimePartVariableId
            ? new(id, RoomId, 7, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }
}
