using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint ChangedContext = 981, ChangedUser = 982, ContextVariableTile = 983, ContextBoxTile = 984, UserVariableTile = 985;

    // A Variable Changed trigger never fires when a context variable is given or changed, whether it picks the variable or the variable's box, and it still saves with the context variable picked. The same trigger on a user variable fires on every give.
    [Fact]
    public async Task VariableChangedNeverFiresForAContextVariable()
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new ChangedContextDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        PlaceDefinition(ChangedContext, "wf_var_context", 0);
        PlaceDefinition(ChangedUser, "wf_var_user", 1);
        var contextVariableTile = ToggleAt(ContextVariableTile, 0, 2);
        var contextBoxTile = ToggleAt(ContextBoxTile, 1, 2);
        var userVariableTile = ToggleAt(UserVariableTile, 2, 2);

        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "give" }, 0, 0, 0);
        PlaceSavedBox(302, "wf_act_give_var", 0, 0, 1);
        AddSpeechBox(321, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "change" }, 0, 1, 0);
        AddSpeechBox(322, "wf_xtra_exec_in_order", new(), 1, 1, 0);
        PlaceSavedBox(323, "wf_act_give_var", 1, 0, 2);
        AddVariableBox(324, "wf_act_change_var_val", [-20, 1, 0, 0, 1, -10, 0], [$"ctx:{ChangedContext}", "n"], 3, 1, 0);
        AddSpeechBox(331, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "user" }, 0, 2, 0);
        PlaceSavedBox(332, "wf_act_give_var", 2, 0, 1);
        PlaceSavedBox(311, "wf_trg_var_changed", 0, 1, 0);
        ToggleStack(312, 0, 1, contextVariableTile);
        PlaceSavedBox(313, "wf_trg_var_changed", 1, 1, 0);
        ToggleStack(314, 1, 1, contextBoxTile);
        PlaceSavedBox(315, "wf_trg_var_changed", 2, 1, 0);
        ToggleStack(316, 2, 1, userVariableTile);

        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(302, 7, -20, 0, 5, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{ChangedContext}", "n", 0));
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(323, 7, -20, 0, 5, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{ChangedContext}", "n", 0));
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(332, 7, 1, 0, 5, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{ChangedUser}", "n", 0));
        await new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket(311, 6, 1, 1, 0, 7, -1, 0, "", 0, 0, 0, 1, $"ctx:{ChangedContext}", 0));
        await new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket(313, 6, 1, 1, 0, 7, -1, 1, "", 1, (int)ChangedContext, 0, 0, 1, "0", 0));
        await new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket(315, 6, 1, 1, 0, 7, -1, 0, "", 0, 0, 0, 1, $"user:{ChangedUser}", 0));
        Assert.Equal(6, _client.Packets.Count(packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.HideWiredConfigComposer));
        wired.FlushFlashes();

        var toggled = new List<uint>();

        foreach (var say in new[] { "give", "change", "user", "give", "change", "user", "give", "change", "user" }) {
            _client.Packets.Clear();
            clock.Now = clock.Now.AddSeconds(3);
            actor.OnChat(0, say, false);
            wired.OnFastCycle();
            wired.OnFastCycle();
            toggled.AddRange(DataUpdates(_client).SelectMany(update => update).Select(entry => entry.Id)
                .Where(id => id is ContextVariableTile or ContextBoxTile or UserVariableTile));
        }

        Assert.Equal([UserVariableTile, UserVariableTile, UserVariableTile], toggled);
    }

    private void PlaceSavedBox(uint id, string name, int x, int y, double height)
    {
        var box = WiredBox(id, name, x, y);
        box.Item.GetZ = height;
        Assert.True(_room.GetWired().AddBox(box));
    }

    private void ToggleStack(uint id, int x, int y, Item target)
    {
        var toggle = WiredBox(id, "wf_act_toggle_state", x, y);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(toggle, new() { IntParams = [0, 100], SelectedItems = [target.Id] },
            out var config, out var error), error);
        toggle.ApplyConfiguration(config);
        toggle.Item.GetZ = 1;
        Assert.True(_room.GetWired().AddBox(toggle));
    }

    private sealed class ChangedContextDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            ChangedContext => new(id, RoomId, 7, "c1", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            ChangedUser => new(id, RoomId, 7, "u1", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }
}
