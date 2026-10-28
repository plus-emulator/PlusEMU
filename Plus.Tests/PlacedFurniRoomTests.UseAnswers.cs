using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private UseFurnitureEvent UseEvent() => new(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));

    // A user's double-click on a gate is answered with an ObjectDataUpdate of the new state, not with a full ObjectUpdate.
    [Fact]
    public async Task DoubleClickingAGateAnswersWithADataUpdateAndNoObjectUpdate()
    {
        AdmitOwner();
        var gate = Furni(260, InteractionType.Gate, WiredBoxType.None);
        gate.Definition.Modes = 2;
        gate.Definition.ItemName = "wf_firegate";
        gate.Definition.Walkable = false;
        gate.Definition.Height = 0;
        gate.ExtraData = FurniExtraData.Load(gate.Definition, "0", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, gate, 2, 2, 0, true, false, false));
        var use = UseEvent();

        foreach (var state in new[] { "1" }) {
            _client.Packets.Clear();

            await use.Parse(_room, _client, ClientPacket((int)gate.Id, 0));

            Assert.Equal(state, gate.LegacyDataString);
            var sent = StateHeightPackets();
            Assert.Equal([ServerPacketHeader.ObjectDataUpdateComposer], sent.Select(packet => packet.Header));
            Assert.Equal(("260", state), ReadLegacyDataUpdate(sent[0].Body));
        }
    }

    // A hidden pyramid does not answer a user's double-click; it only flips on its own timer.
    [Fact]
    public async Task DoubleClickingAHiddenPyramidChangesNothing()
    {
        AdmitOwner();
        var pyramid = HiddenPyramid("1");
        _client.Packets.Clear();

        await UseEvent().Parse(_room, _client, ClientPacket((int)pyramid.Id, 0));

        for (var tick = 0; tick < 4; tick++) {
            ExecutorTick();
        }

        Assert.Equal("1", pyramid.LegacyDataString);
        Assert.Empty(StateHeightPackets());
    }

    // A recolorable with one state (the stairs) still answers each double-click with an ObjectDataUpdate of its unchanged data.
    [Fact]
    public async Task DoubleClickingAOneStateRecolorableAnswersWithItsData()
    {
        AdmitOwner();
        var definition = RecolorableFurniTests.Block(1);
        definition.ItemName = "bc_stairs_recolorable";
        definition.Height = 0.5;
        var stairs = PlacedRecolorable(261, definition: definition);
        var use = UseEvent();

        for (var click = 0; click < 3; click++) {
            _client.Packets.Clear();

            await use.Parse(_room, _client, ClientPacket((int)stairs.Id, 0));

            Assert.Equal("0", stairs.LegacyDataString);
            var sent = StateHeightPackets();
            Assert.Equal([ServerPacketHeader.ObjectDataUpdateComposer], sent.Select(packet => packet.Header));
            Assert.Equal(("261", "0"), ReadDataUpdate(sent[0].Body));
        }
    }

    private static (string Id, string State) ReadLegacyDataUpdate(byte[] body)
    {
        var input = new FlashIncomingPacket { Buffer = body };
        var id = input.ReadString();
        Assert.Equal((int)Plus.HabboHotel.Items.DataFormat.FurniDataStructure.Legacy, input.ReadInt());

        return (id, input.ReadString());
    }
}
