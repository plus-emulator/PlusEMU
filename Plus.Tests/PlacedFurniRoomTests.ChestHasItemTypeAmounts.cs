using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Chest Has Item Type compares the number of furni of the picked types in its chests. Observed on AIR WIN63-202610201032-107964540: with
// the chest selection on the second picks and nothing picked there, the condition counts 0 and applies the comparison, so "equal to 1",
// "more than 0", "more than 1" and "less than 0" fail.
public partial class PlacedFurniRoomTests
{
    private const uint ChestId = 640;
    private const uint SampleId = 641;
    private const uint TypeConditionId = 302;

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(0, 1, true)]
    [InlineData(0, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(3, 1, true)]
    [InlineData(4, 1, true)]
    [InlineData(5, 0, true)]
    [InlineData(5, 1, false)]
    public async Task ChestHasItemTypeWithNoChestSelectedComparesZero(int comparison, int amount, bool passes)
    {
        var (wired, actor) = PrepareChestSpeech([]);

        await SaveTypeCondition(amount, comparison, picks: [ChestId, SampleId], secondary: []);
        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([passes ? "shown" : "pulse"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    [Theory]
    [InlineData(1, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 0, false)]
    [InlineData(2, 1, false)]
    [InlineData(0, 1, true)]
    [InlineData(0, 0, false)]
    [InlineData(3, 0, true)]
    [InlineData(3, 1, true)]
    [InlineData(4, 1, true)]
    [InlineData(5, 0, true)]
    [InlineData(5, 1, false)]
    public async Task ChestHasItemTypeOnAnEmptyChestComparesZero(int comparison, int amount, bool passes)
    {
        var (wired, actor) = PrepareChestSpeech([]);

        await SaveTypeCondition(amount, comparison, picks: [SampleId], secondary: [ChestId]);
        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([passes ? "shown" : "pulse"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(1, 0, false)]
    [InlineData(0, 2, false)]
    public async Task ChestHasItemTypeCountsTheFurniOfThePickedTypeInTheChest(int comparison, int amount, bool passes)
    {
        var (wired, actor) = PrepareChestSpeech([StockOfTheSampleType(900), StockOfTheSampleType(901)]);

        await SaveTypeCondition(amount, comparison, picks: [SampleId], secondary: [ChestId]);
        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([passes ? "shown" : "pulse"], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    private (WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor) PrepareChestSpeech(InventoryItem[] stock)
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        var store = new G1ChestStore();
        typeof(WiredComponent).GetField("_chests", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new WiredChestRoom(_room, store, TimeProvider.System, evt => wired.Dispatch(evt)));
        var chest = Furni(ChestId, InteractionType.WiredChestFurni, WiredBoxType.None);
        chest.Definition.ItemName = "wf_storage_furni1";
        chest.Definition.SpriteId = 20;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, chest, 3, 3, 0, true, false, false));
        store.AddChest(ChestId, WiredChestKind.Furni, 0, stock).OwnerId = 7;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(SampleId, InteractionType.None, WiredBoxType.None), 3, 2, 0, true, false, false));
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var condition = WiredBox(TypeConditionId, "wf_cnd_chest_has_item_type", 1, 1);
        condition.Item.GetZ = 1;
        Assert.True(wired.AddBox(condition));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "shown" }, 2);

        return (wired, actor);
    }

    private static InventoryItem StockOfTheSampleType(uint id) => new()
    {
        Id = id,
        Definition = new() { Id = 10, SpriteId = 10, Type = ItemType.Floor, AllowTrade = true }
    };

    // The raw condition save the client sends: ints [amount, constant operand, reference target, comparison], the first picks, the
    // quantifier, the furni sources [types, chests, variable furni], the user source, no variable, then the second picks.
    private async Task SaveTypeCondition(int amount, int comparison, uint[] picks, uint[] secondary)
    {
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());

        await new SaveWiredConditionConfigEvent(service).Parse(_client, ClientPacket([(int)TypeConditionId, 4, amount, 0, -20, comparison, "",
            picks.Length, .. picks.Select(id => (object)(int)id), 0, 3, 100, 101, 0, 1, 0, 0, secondary.Length, .. secondary.Select(id => (object)(int)id)]));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        _client.Packets.Clear();
    }
}
