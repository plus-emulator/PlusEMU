using System.Reflection;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (seven saves that repeat an id, three runs, and after the room unloaded): the pick order of a card
// follows a set built twice, the first time sized from the number of ids sent, repeats counted. A save of 12 ids with 11 different ones reopens
// in another order than the same 11 ids sent once, and the room unloading changed nothing. Expected orders come from the real JDK 21 HashSet
// (legacy copy constructor sizing).
public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task ASaveThatRepeatsAnIdReopensInTheOrderOfTheSentCountAlsoAfterReloading()
    {
        int[] sent = [734, 703, 725, 704, 720, 723, 737, 752, 709, 720, 741, 706];
        int[] reopened = [704, 720, 752, 737, 706, 723, 709, 741, 725, 734, 703];
        PickOrderRoom([.. sent.Distinct()], null);
        var store = new CanonicalStore(null);
        var service = new WiredConfigurationService(store, null!, null!, TestLogging.For<WiredConfigurationService>());

        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket([(int)PickOrderBox, 1, 0, "", sent.Length, .. sent.Cast<object>(), 0, 1, 100, 0, 0, 0]));

        Assert.Equal(reopened, await OpenPicks());

        // The row as the database holds it: the native record as JSON.
        var row = JsonSerializer.Serialize(Assert.Single(store.Saves).Origin!.Native);
        ReloadPickOrderRoom([.. sent.Distinct()], row);

        Assert.Equal(reopened, await OpenPicks());
    }

    private void ReloadPickOrderRoom(int[] picks, string row)
    {
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_room.GetWired(), new RowStore(row));
        var items = picks.Select((id, tile) =>
        {
            var item = Furni((uint)id, InteractionType.None, WiredBoxType.None);
            item.RoomId = RoomId;
            item.GetX = tile % 4;
            item.GetY = tile / 4 % 4;

            return item;
        }).ToList();
        var box = Furni(PickOrderBox, InteractionType.WiredEffect, WiredBoxType.None);
        box.RoomId = RoomId;
        box.Definition.InteractionName = box.Definition.ItemName = "wf_act_toggle_state";
        items.Add(box);
        _room.GetRoomItemHandler().LoadFurniture(items);
        Assert.True(_room.GetWired().TryGet(PickOrderBox, out _));
        _client.Packets.Clear();
    }

    private sealed class RowStore(string row) : IWiredConfigurationStore
    {
        public WiredConfiguration? Load(uint itemId, WiredBoxDescriptor descriptor) => WiredConfigurationStore.DecodeRow(itemId, descriptor.CanonicalName, row);
        public void Save(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration configuration) => throw new InvalidOperationException("Unexpected save.");
        public void Reset(IReadOnlyCollection<uint> itemIds) => throw new InvalidOperationException("Unexpected reset.");
    }
}
