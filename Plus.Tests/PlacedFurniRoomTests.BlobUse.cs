using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private void StartGameCounter(uint id, WiredClockOrigin origin = WiredClockOrigin.Unknown)
    {
        var counter = Furni(id, InteractionType.None, WiredBoxType.None);
        counter.Definition.ItemName = "wf_game_upcounter1";
        counter.Definition.Modes = 2;
        counter.ExtraData = FurniExtraData.Load(counter.Definition, "0", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, counter, 0, 1, 0, true, false, false));
        _room.GetWired().AttachRoomItem(counter);
        var counters = (WiredCounterController)typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent)
            .GetField("_counters", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(_room.GetWired())!;
        Assert.True(counters.Control(counter, 0, 0, origin));
        Assert.True(_room.GetWired().GameCounterRunning);
    }

    // While a Wired game counter is counting, a user's double-click on a blob changes its state on the next room tick, in an ObjectsDataUpdate, and sends no data update at once.
    [Theory]
    [InlineData("wf_blob")]
    [InlineData("wf_blob2")]
    public async Task DoubleClickingABlobInARunningGameChangesItOnTheNextRoomTickInABatch(string name)
    {
        AdmitOwner();
        StartGameCounter(264);
        var blob = Furni(262, InteractionType.None, WiredBoxType.None);
        blob.Definition.Modes = 2;
        blob.Definition.ItemName = name;
        blob.ExtraData = FurniExtraData.Load(blob.Definition, "0", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, blob, 2, 2, 0, true, false, false));
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));

        foreach (var state in new[] { "1", "0", "1" }) {
            _client.Packets.Clear();

            await use.Parse(_room, _client, ClientPacket((int)blob.Id, 0));

            Assert.Equal(StateHeightPackets().Where(packet => packet.Header != ServerPacketHeader.ObjectsDataUpdateComposer), []);
            Assert.NotEqual(state, blob.LegacyDataString);

            _room.GetWired().OnCycle();

            Assert.Equal(state, blob.LegacyDataString);
            Assert.Contains(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
            Assert.DoesNotContain(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        }
    }

    // Without a running game counter a blob does not answer a user's double-click.
    [Theory]
    [InlineData("wf_blob")]
    [InlineData("wf_blob2")]
    public async Task DoubleClickingABlobWithoutAGameChangesNothing(string name)
    {
        AdmitOwner();
        var blob = Furni(262, InteractionType.None, WiredBoxType.None);
        blob.Definition.Modes = 2;
        blob.Definition.ItemName = name;
        blob.ExtraData = FurniExtraData.Load(blob.Definition, "0", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, blob, 2, 2, 0, true, false, false));
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));
        _client.Packets.Clear();

        await use.Parse(_room, _client, ClientPacket((int)blob.Id, 0));
        _room.GetWired().OnCycle();

        Assert.Equal("0", blob.LegacyDataString);
        Assert.Empty(StateHeightPackets());
    }

    // A Wired start of the game counter sets a blob to 0 on the next room tick, so the first double-click of the game answers 1.
    [Theory]
    [InlineData("wf_blob")]
    [InlineData("wf_blob2")]
    public async Task ABlobIsResetWhenAWiredStartBeginsTheGameAndThenAnswersTheFirstClickWithOne(string name)
    {
        AdmitOwner();
        var blob = Furni(262, InteractionType.None, WiredBoxType.None);
        blob.Definition.Modes = 2;
        blob.Definition.ItemName = name;
        blob.ExtraData = FurniExtraData.Load(blob.Definition, "1", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, blob, 2, 2, 0, true, false, false));
        StartGameCounter(264, WiredClockOrigin.ModernWired);
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));
        _client.Packets.Clear();

        _room.GetWired().OnCycle();

        Assert.Equal("0", blob.LegacyDataString);
        Assert.Contains(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        _client.Packets.Clear();

        await use.Parse(_room, _client, ClientPacket((int)blob.Id, 0));
        _room.GetWired().OnCycle();

        Assert.Equal("1", blob.LegacyDataString);
    }
}
