using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Toggle State reopens its picks in the iteration order of a Java HashSet<Integer> of the saved ids (wall picks negative), not in the order the client saved them.
// Ids that share a bucket keep the saved order. Expected orders come from that model.
public partial class PlacedFurniRoomTests
{
    private const uint PickOrderBox = 601;

    [Theory]
    [InlineData(new[] { 618, 605, -611, 602 }, new[] { -611, 618, 602, 605 })]
    [InlineData(new[] { 602, -611, 605, 618 }, new[] { -611, 602, 618, 605 })]
    [InlineData(new[] { 631, 630, 629, 628, 627, 626, 625, 624, 623, 622, 621, 620, -641, -640 },
        new[] { -641, 620, 621, 622, 623, 624, 625, 626, 627, 628, 629, 630, 631, -640 })]
    public async Task ToggleStateReopensItsPicksInHashSetOrder(int[] sent, int[] reopened)
    {
        PickOrderRoom(sent, null);
        var service = new WiredConfigurationService(new CanonicalStore(null), null!, null!, TestLogging.For<WiredConfigurationService>());

        await new SaveWiredEffectConfigEvent(service).Parse(_client,
            ClientPacket([(int)PickOrderBox, 1, 0, "", sent.Length, .. sent.Cast<object>(), 0, 1, 100, 0, 0, 0]));

        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal(reopened, await OpenPicks());
    }

    // The stored row keeps the order the client saved; only what reopen sends follows the set order, also after the box loads again.
    [Fact]
    public async Task AToggleStateRowKeepsTheSavedOrderAndReopensInHashSetOrderAfterReloading()
    {
        PickOrderRoom([618, 605, -611, 602], null);
        var store = new CanonicalStore(null);
        var service = new WiredConfigurationService(store, null!, null!, TestLogging.For<WiredConfigurationService>());
        await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)PickOrderBox, 1, 0, "", 4, 618, 605, -611, 602, 0, 1, 100, 0, 0, 0));
        var saved = Assert.Single(store.Saves);
        Assert.Equal(new uint[] { 618, 605, 611, 602 }, saved.SelectedItems);

        PickOrderRoom([618, 605, -611, 602], saved);

        Assert.Equal(new[] { -611, 618, 602, 605 }, await OpenPicks());
    }

    // Sent and reopened orders with Habbo's own ids.
    [Theory]
    [InlineData(new[] { 2147418112, 2147418119, -2147418130, 2147418117 }, new[] { 2147418119, 2147418117, -2147418130, 2147418112 })]
    [InlineData(new[] { -2147418131, 2147418115, -2147418129, 2147418123, 2147418113 },
        new[] { 2147418123, 2147418115, -2147418131, 2147418113, -2147418129 })]
    [InlineData(new[] { 2147418112, 2147418113, 2147418114, 2147418115, 2147418116, 2147418117, 2147418118, 2147418119, 2147418120,
        2147418121, 2147418122, 2147418123, -2147418129, -2147418130 },
        new[] { -2147418130, -2147418129, 2147418123, 2147418122, 2147418121, 2147418120, 2147418119, 2147418118, 2147418117, 2147418116,
            2147418115, 2147418114, 2147418113, 2147418112 })]
    [InlineData(new[] { 2147418116, 2147418115, -2147418117 }, new[] { 2147418116, -2147418117, 2147418115 })]
    [InlineData(new[] { 2147418115, -2147418117, 2147418116 }, new[] { -2147418117, 2147418116, 2147418115 })]
    public void ToggleStateFrameCarriesHabboIdsInHashSetOrder(int[] sent, int[] reopened)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_toggle_state", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [.. sent.Select(id => new WiredNativeItemReference((uint)Math.Abs(id), id < 0))]
        };
        var packet = new HabbiconTestSupport.RecordingPacket();

        WiredLegacyProtocol.Write(packet, 9, 100, descriptor, new(), 20, [], native);

        Assert.Equal(sent.Length, packet.Writes[1]);
        Assert.Equal(reopened, packet.Writes.Skip(2).Take(sent.Length).Cast<int>());
    }

    // Loads the room with the picks and the Toggle State box, whose settings come from the given stored row.
    private void PickOrderRoom(int[] picks, WiredConfiguration? stored)
    {
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(_room.GetWired(), new CanonicalStore(stored));
        var items = picks.Select((id, tile) =>
        {
            var item = Furni((uint)Math.Abs(id), InteractionType.None, WiredBoxType.None, id < 0 ? ItemType.Wall : ItemType.Floor);
            item.RoomId = RoomId;
            item.WallCoordinates = id < 0 ? $":w=2,1 l={tile},16 l" : "";
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

    // The raw WIRED_OPEN packet, then the picks list of the effect editor frame that follows the furni limit.
    private async Task<int[]> OpenPicks()
    {
        _client.Packets.Clear();
        await new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)PickOrderBox));
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredEffectConfigComposer).Body };
        frame.ReadInt();

        return [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];
    }
}
