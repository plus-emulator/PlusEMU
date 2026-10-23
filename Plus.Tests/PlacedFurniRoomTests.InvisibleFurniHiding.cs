using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlaceInvisible(uint id, int x, string name = "room_invisible_block")
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.ItemName = name;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, x, 2, 0, true, false, true));

        return item;
    }

    private static List<uint> RemovedIds(byte[] body)
    {
        var count = BinaryPrimitives.ReadInt32BigEndian(body);
        var ids = Enumerable.Range(0, count).Select(i => BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(4 + i * 4))).ToList();
        Assert.Equal(-1, BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(4 + count * 4)));
        Assert.Equal(1, body[8 + count * 4]);

        return ids;
    }

    // Switching the Invisible Furni Controller on removes the room_invisible furni from the clients with one ObjectRemoveMultiple
    // (picker -1, flag on); other furni stay.
    [Fact]
    public void SwitchingTheInvisibleFurniControllerOnRemovesTheInvisibleFurniFromTheClients()
    {
        var controller = PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: false);
        PlaceInvisible(71, 2);
        PlaceInvisible(72, 3, "room_invisible_sit_tile");
        PlaceInvisible(73, 0, "colortile");
        _client.Packets.Clear();

        controller.Interactor.OnTrigger(_client, controller, 0, true);

        Assert.Equal([71u, 72u], RemovedIds(Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectRemoveMultipleComposer).Body));
        Assert.Equal(new byte[] { 0, 0, 0, 1, 0 }, Assert.Single(StatesPackets()));
    }

    // Switching it off sends the flag and one Objects packet that adds the hidden furni.
    [Fact]
    public void SwitchingTheInvisibleFurniControllerOffAddsTheInvisibleFurniAgain()
    {
        var controller = PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: false);
        PlaceInvisible(71, 2);
        controller.Interactor.OnWiredTrigger(controller);
        _client.Packets.Clear();

        controller.Interactor.OnWiredTrigger(controller);

        Assert.Equal(new byte[] { 0, 0, 0, 0, 0 }, Assert.Single(StatesPackets()));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectRemoveMultipleComposer);
    }

    // An invisible furni placed while the controller is on is accepted, but no ObjectAdd is sent; it appears when the controller goes off.
    [Fact]
    public void AnInvisibleFurniPlacedWhileTheControllerIsOnIsAcceptedWithoutAnObjectAdd()
    {
        PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: true);
        var item = Furni(71, InteractionType.None, WiredBoxType.None);
        item.Definition.ItemName = "room_invisible_block";

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 2, 2, 0, true, false, true));

        Assert.NotNull(_room.GetRoomItemHandler().GetItem(71));
        Assert.DoesNotContain(ServerPacketHeader.ObjectAddComposer, _client.Sent);
    }

    // The room entry lists no hidden furni while the controller is on.
    [Fact]
    public void TheRoomEntryLeavesOutTheHiddenInvisibleFurni()
    {
        PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: true);
        PlaceInvisible(71, 2);
        PlaceInvisible(72, 3, "colortile");

        Assert.Equal([70u, 72u], _room.VisibleFloorItems.Select(item => item.Id).OrderBy(id => id));
    }

    [Fact]
    public async Task PickingTheControllerUpWhileOnSendsTheFlagOnAndThenOffWithTheFurniAdded()
    {
        PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: true);
        PlaceInvisible(71, 2);
        _client.Packets.Clear();

        await PickupObject().Parse(_client, ClientPacket(0, 70));

        Assert.Equal([new byte[] { 0, 0, 0, 1, 0 }, new byte[] { 0, 0, 0, 0, 0 }], StatesPackets());
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsComposer);
    }

    [Fact]
    public void AnAreaHiderWithTheInvisibilityOptionIsHiddenToo()
    {
        var controller = PlaceConfiguration(70, InteractionType.InvisibleFurniControl, on: false);
        var hider = Furni(71, InteractionType.AreaHide, WiredBoxType.None);
        hider.ExtraData = Plus.HabboHotel.Items.AreaHide.AreaHideState.Load("0\n0\n0\n0\n0\n1\n1\n0");
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, hider, 2, 2, 0, true, false, false));
        _client.Packets.Clear();

        controller.Interactor.OnTrigger(_client, controller, 0, true);

        Assert.Equal([71u], RemovedIds(Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectRemoveMultipleComposer).Body));
    }
}
