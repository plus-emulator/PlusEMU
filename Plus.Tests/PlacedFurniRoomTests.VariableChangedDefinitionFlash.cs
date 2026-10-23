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
    private const uint FlashedContext = 994, FlashedFire = 995, FlashedPotion = 996;

    // One execution gives two user variables, so a Variable Changed stack that gives a context variable runs twice in the same tick. The one ObjectsDataUpdate of that tick flips the context variable's box once and each given variable's box once.
    [Fact]
    public async Task TwoVariableChangedRunsInOneTickFlipTheContextBoxOnce()
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new FlashedDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        PlaceDefinition(FlashedFire, "wf_var_user", 0);
        PlaceDefinition(FlashedPotion, "wf_var_user", 1);
        PlaceDefinition(FlashedContext, "wf_var_context", 2);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        PlaceFlashedBox(302, "wf_act_give_var", 1, 1);
        PlaceFlashedBox(303, "wf_act_give_var", 1, 2);
        PlaceFlashedBox(311, "wf_trg_var_changed", 3, 0);
        PlaceFlashedBox(312, "wf_act_give_var", 3, 1);

        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(302, 7, 1, 0, 44, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{FlashedFire}", "n", 0));
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(303, 7, 1, 0, 55, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{FlashedPotion}", "n", 0));
        await new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket(311, 6, 1, 1, 0, 7, -1, 1, "", 2, (int)FlashedFire, (int)FlashedPotion, 0, 0, 1, "0", 0));
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(312, 7, -20, 0, 1, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{FlashedContext}", "n", 0));
        Assert.Equal(4, _client.Packets.Count(packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.HideWiredConfigComposer));
        wired.FlushFlashes();

        var states = new List<string>();

        foreach (var expected in new[] { "6", "7" }) {
            _client.Packets.Clear();
            clock.Now = clock.Now.AddSeconds(3);
            actor.OnChat(0, "go", false);
            wired.OnFastCycle();

            var update = Assert.Single(DataUpdates(_client), batch => batch.Any(entry => entry.Id == FlashedContext));
            Assert.Single(update, entry => entry.Id == FlashedContext);
            Assert.Equal(expected, update.Single(entry => entry.Id == FlashedFire).State);
            Assert.Equal(expected, update.Single(entry => entry.Id == FlashedPotion).State);
            states.Add(update.Single(entry => entry.Id == FlashedContext).State);
        }

        Assert.Equal(["6", "7"], states);
    }

    private void PlaceDefinition(uint id, string name, int x)
    {
        var item = Furni(id, InteractionType.WiredVariable, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = name;
        item.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, 3, 0, true, false, false));
    }

    private void PlaceFlashedBox(uint id, string name, int x, double height)
    {
        var box = WiredBox(id, name, x, 1);
        box.Item.GetZ = height;
        Assert.True(_room.GetWired().AddBox(box));
    }

    private sealed class FlashedDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            FlashedContext => new(id, RoomId, 7, "ctx", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            FlashedFire => new(id, RoomId, 7, "fire", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            FlashedPotion => new(id, RoomId, 7, "potion", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }
}
