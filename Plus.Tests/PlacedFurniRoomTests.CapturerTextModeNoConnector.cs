using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using System.Reflection;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint CaptureNoConnector = 961, CaptureNumber = 962, CaptureWithConnector = 963, CaptureConnector = 964;
    private const uint CaptureTextTile = 965, CaptureNumberTile = 966, CaptureLabelTile = 967;

    // Observed on AIR WIN63-202610201032-107964540: a capturer in text mode over a context variable with no Text Connector on its tile
    // never lets the Says trigger fire, for a number or a word. In number mode the same shape fires for a number only, and in text mode
    // over a variable with the connector "7=seven" only the label fires.
    [Fact]
    public async Task ATextModeCapturerWithoutAConnectorCapturesNothing()
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new CaptureDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());

        CaptureDefinition(CaptureNoConnector, 0);
        CaptureDefinition(CaptureNumber, 1);
        CaptureDefinition(CaptureWithConnector, 2);
        PlaceSavedBox(CaptureConnector, "wf_xtra_var_text_connector", 2, 3, 1);
        var textTile = ToggleAt(CaptureTextTile, 0, 2);
        var numberTile = ToggleAt(CaptureNumberTile, 1, 2);
        var labelTile = ToggleAt(CaptureLabelTile, 2, 2);

        AddSpeechBox(341, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "tc price #(amount)" }, 0, 0, 0);
        PlaceSavedBox(342, "wf_xtra_text_input_variable", 0, 0, 1);
        ToggleStack(343, 0, 0, textTile);
        AddSpeechBox(351, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "tn price #(amount)" }, 0, 1, 0);
        PlaceSavedBox(352, "wf_xtra_text_input_variable", 1, 0, 1);
        ToggleStack(353, 1, 0, numberTile);
        AddSpeechBox(361, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "tk price #(amount)" }, 0, 2, 0);
        PlaceSavedBox(362, "wf_xtra_text_input_variable", 2, 0, 1);
        ToggleStack(363, 2, 0, labelTile);

        // The raw saves: the connector's text, then each capturer with its mode (1 is text, 0 is number), name and context variable.
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket((int)CaptureConnector, 0, "7=seven", 0, 0, 0, 0, 0));
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(342, 1, 1, "amount", 0, 0, 0, 1, $"ctx:{CaptureNoConnector}", 0));
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(352, 1, 0, "amount", 0, 0, 0, 1, $"ctx:{CaptureNumber}", 0));
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket(362, 1, 1, "amount", 0, 0, 0, 1, $"ctx:{CaptureWithConnector}", 0));
        Assert.Equal(4, _client.Packets.Count(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer));
        wired.FlushFlashes();

        var fired = new List<string>();

        foreach (var prefix in new[] { "tc", "tn", "tk" }) {
            foreach (var word in new[] { "7", "seven", "-3", "abc", "12" }) {
                _client.Packets.Clear();
                clock.Now = clock.Now.AddSeconds(3);
                actor.OnChat(0, $"{prefix} price {word}", false);
                wired.OnFastCycle();
                wired.OnFastCycle();

                if (DataUpdates(_client).SelectMany(update => update).Any(entry => entry.Id is CaptureTextTile or CaptureNumberTile or CaptureLabelTile)) {
                    fired.Add($"{prefix} {word}");
                }
            }
        }

        Assert.Equal(["tn 7", "tn -3", "tn 12", "tk seven"], fired);
    }

    private void CaptureDefinition(uint id, int x)
    {
        PlaceDefinition(id, "wf_var_context", x);
        var item = Assert.IsType<Item>(_room.GetRoomItemHandler().GetItem(id));
        Assert.True(WiredBoxRegistry.TryGet("wf_var_context", out var descriptor));
        var definition = new WiredVariableDefinitionBox(_room, item, descriptor);
        WiredNativeTestSupport.InstallLegacyVariableDraft(definition, new() { IntParams = [1], Text = "nc" + x });
        _room.GetWired().Variables.ConfigurationLoaded(definition);
    }

    private sealed class CaptureDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id is CaptureNoConnector or CaptureNumber or CaptureWithConnector
            ? new(id, RoomId, 7, "nc" + id, WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true)
            : null;
    }
}
