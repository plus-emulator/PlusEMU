using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint ShownContextVariable = 994;

    // Observed on AIR WIN63-202610201032-107964540: without Execute in Order, a Show Message whose number-mode placeholder reads the
    // context variable a Give Variable of the same firing writes prints the new value, with the message below the Give (11 of 11) and
    // above it (11 of 11).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowMessagePrintsTheValueAGiveOfTheSameFiringWrote(bool showBelow)
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new ShownContextDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        var engine = typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        PlaceUnsaved(302, "wf_act_show_message", showBelow ? 1 : 2);
        PlaceUnsaved(303, "wf_act_give_var", showBelow ? 2 : 1);
        PlaceUnsaved(304, "wf_xtra_text_output_variable", 3);

        await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(302, 3, 0, 202, -1, "v=$(cv)", 0, 0, 0, 1, 0, 0, 0));
        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket(303, 7, -20, 0, 5, 0, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{ShownContextVariable}", "n", 0));
        await new SaveWiredAddonConfigEvent(service).Parse(_client,
            ClientPacket(304, 3, 0, -20, 0, "cv\t,", 0, 1, 0, 1, 0, 1, $"ctx:{ShownContextVariable}", 0));
        Assert.Equal(3, _client.Packets.Count(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer));
        _client.Packets.Clear();

        for (var firing = 0; firing < 16; firing++) {
            clock.Now = clock.Now.AddSeconds(1);
            actor.OnChat(0, "go", false);
            wired.OnFastCycle();
        }

        Assert.Equal(Enumerable.Repeat("v=5", 16), SpeechMessages(ServerPacketHeader.WhisperComposer));
    }

    private void PlaceUnsaved(uint id, string name, double height)
    {
        var box = WiredBox(id, name, 1, 1);
        box.Item.GetZ = height;
        Assert.True(_room.GetWired().AddBox(box));
    }

    private sealed class ShownContextDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id == ShownContextVariable
            ? new(id, RoomId, 7, "cv", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true) : null;
    }
}
