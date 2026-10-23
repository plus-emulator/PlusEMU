using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A Change Variable Value that sets @state to 1 on picked furni changes a Wired floor switch, while the picked Wired boxes and antennas stay at 0.
    [Fact]
    public async Task ChangeVariableValueLeavesTheStateOfPickedWiredBoxes()
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new SameTickVariableDirectory(), new MemoryWiredVariableStore(), TimeProvider.System,
            new RoomWiredBuiltinVariables(_room));
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);
        PlaceUnsaved(302, "wf_act_change_var_val", 1);
        var box = WiredBox(310, "wf_act_move_rotate", 2, 2).Item;
        var antenna = Furni(311, InteractionType.None, WiredBoxType.None);
        antenna.Definition.ItemName = "wf_antenna2";
        antenna.Definition.InteractionName = "antenna";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, antenna, 3, 3, 0, true, false, false));
        var floorSwitch = Furni(312, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, floorSwitch, 1, 2, 0, true, false, false));

        foreach (var item in new[] { box, antenna, floorSwitch }) {
            item.Definition.Modes = 2;
            item.ExtraData = new LegacyDataFormat { Data = "0" };
        }

        await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(302, 7, 0, 0, 0, 0, 1, -10, 0, "",
            3, (int)box.Id, (int)antenna.Id, (int)floorSwitch.Id, 0, 2, 100, 200, 2, 0, 200, 2, "-110", "0", 0));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);

        clock.Now = clock.Now.AddSeconds(1);
        actor.OnChat(0, "go", false);
        wired.OnFastCycle();

        Assert.Equal("1", floorSwitch.LegacyDataString);
        Assert.Equal("0", box.LegacyDataString);
        Assert.Equal("0", antenna.LegacyDataString);
    }
}
