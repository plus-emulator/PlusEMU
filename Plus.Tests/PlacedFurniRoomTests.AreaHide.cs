using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Rooms.Furni;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.AreaHide;
using Plus.HabboHotel.Items.Wired;
using Xunit;
using static Plus.Tests.HabbiconTestSupport;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item PlaceAreaHider(uint id, string values = "0\n0\n0\n0\n0\n0\n1\n0")
    {
        Viewer();
        var hider = Furni(id, InteractionType.AreaHide, WiredBoxType.None);
        hider.ExtraData = AreaHideState.Load(values);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, hider, 0, 0, 0, true, false, true));
        _client.Packets.Clear();
        _client.Sent.Clear();

        return hider;
    }

    private Item PlaceNormal(uint id, int x, int y)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.ItemName = "colortile";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, x, y, 0, true, false, true));

        return item;
    }

    private Task SaveAreaHide(uint id, int x, int y, int w, int l, bool invisibility = false, bool wallItems = true, bool invert = false) =>
        new SetAreaHideEvent().Parse(_client, ClientPacket((int)id, x, y, w, l, invisibility, wallItems, invert));

    private static int[] Rect(Item hider) => Assert.IsType<Plus.HabboHotel.Items.DataFormat.IntArrayDataFormat>(hider.ExtraData).Data.ToArray();

    private List<uint> Removed() => _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectRemoveMultipleComposer)
        .SelectMany(packet => RemovedIds(packet.Body)).ToList();

    // Observed on AIR WIN63-202610201032-107964540: the widget saves with SetAreaHide (3831): id, x, y, width, length, invisibility, wall
    // items, invert. The answer is one ObjectDataUpdate and nothing else while the hider is off.
    [Fact]
    public async Task SetAreaHideSavesTheRectangleAndOptionsAndAnswersWithOneDataUpdate()
    {
        var hider = PlaceAreaHider(100);

        await SaveAreaHide(100, 1, 2, 3, 4, invisibility: true, wallItems: false, invert: true);

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 1, 0, 1 }, Rect(hider));
        Assert.Equal([ServerPacketHeader.ObjectDataUpdateComposer], _client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: negative values become 0 and a width or length over 64 becomes 64. The origin is not
    // limited.
    [Fact]
    public async Task SetAreaHideClampsTheSizeAndKeepsTheOrigin()
    {
        var hider = PlaceAreaHider(100);

        await SaveAreaHide(100, -1, 50, -3, 100);
        Assert.Equal(new[] { 0, 0, 50, 0, 64, 0, 1, 0 }, Rect(hider));

        await SaveAreaHide(100, 0, 0, 21, 1000000);
        Assert.Equal(new[] { 0, 0, 0, 21, 64, 0, 1, 0 }, Rect(hider));
    }

    [Fact]
    public async Task SetAreaHideNeedsRightsAnAreaHiderAndTheRoom()
    {
        var hider = PlaceAreaHider(100);
        var other = PlaceNormal(101, 2, 2);
        _room.OwnerId = 99;
        _room.OwnerName = "someone";
        _client.Sent.Clear();

        await SaveAreaHide(100, 1, 1, 2, 2);
        await SaveAreaHide(101, 1, 1, 2, 2);

        Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 1, 0 }, Rect(hider));
        Assert.IsNotType<Plus.HabboHotel.Items.DataFormat.IntArrayDataFormat>(other.ExtraData);
        Assert.Empty(_client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: AreaHide (2763) has exactly 7 fields: id, on, x, y, width, length, invert.
    [Fact]
    public void TheAreaHideMessageHasSevenFields()
    {
        var item = PlaceAreaHider(100, "1\n2\n3\n4\n5\n1\n0\n1");
        var packet = new RecordingPacket();

        new Plus.Communication.Packets.Outgoing.Rooms.Furni.AreaHideComposer(item.Id, Assert.IsType<AreaHideValues>(ReadValues(item))).Compose(packet);

        Assert.Equal(new object[] { 100u, true, 2, 3, 4, 5, true }, packet.Writes);
    }

    private static AreaHideValues ReadValues(Item item)
    {
        Assert.True(AreaHideState.TryRead(item, out var state));

        return state;
    }

    // Observed on AIR WIN63-202610201032-107964540: turning the hider on sends the data update, then removes the furni inside the
    // rectangle in one ObjectRemoveMultiple (picker -1, flag on), then AreaHide, then the tiles as a height map update with 0xFFFF. The
    // hider itself and the avatars stay.
    [Fact]
    public async Task TurningTheAreaHiderOnRemovesTheFurniInsideTheRectangle()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        PlaceNormal(102, 2, 2);
        PlaceNormal(103, 3, 3);
        await SaveAreaHide(100, 1, 1, 2, 2);
        _client.Packets.Clear();
        _client.Sent.Clear();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        Assert.Equal([101u, 102u], Removed());
        var order = _client.Sent.Where(id => id is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectRemoveMultipleComposer
            or ServerPacketHeader.AreaHideComposer or ServerPacketHeader.HeightMapUpdateComposer).ToArray();
        Assert.Equal([ServerPacketHeader.ObjectDataUpdateComposer, ServerPacketHeader.ObjectRemoveMultipleComposer, ServerPacketHeader.AreaHideComposer,
            ServerPacketHeader.HeightMapUpdateComposer], order);
        var area = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.AreaHideComposer).Body;
        Assert.Equal(new byte[] { 0, 0, 0, 100, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0 }, area);
        var tiles = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HeightMapUpdateComposer).Body;
        Assert.Equal(4, tiles[0]);
        Assert.All(Enumerable.Range(0, 4), i => Assert.Equal((short)-1, BinaryPrimitives.ReadInt16BigEndian(tiles.AsSpan(1 + i * 4 + 2))));
    }

    // Observed on AIR WIN63-202610201032-107964540: turning it off adds the furni again with one Objects packet, sends AreaHide off and the
    // tiles with their heights.
    [Fact]
    public async Task TurningTheAreaHiderOffAddsTheFurniAgain()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        _client.Packets.Clear();
        _client.Sent.Clear();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsComposer);
        Assert.Equal(0, Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.AreaHideComposer).Body[4]);
        var tiles = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HeightMapUpdateComposer).Body;
        Assert.Equal(4, tiles[0]);
        Assert.All(Enumerable.Range(0, 4), i => Assert.NotEqual((short)-1, BinaryPrimitives.ReadInt16BigEndian(tiles.AsSpan(1 + i * 4 + 2))));
    }

    // Observed on AIR WIN63-202610201032-107964540: with invert on, everything outside the rectangle is hidden and the hider stays.
    [Fact]
    public async Task AnInvertedAreaHiderHidesTheFurniOutsideTheRectangle()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        PlaceNormal(102, 3, 3);
        await SaveAreaHide(100, 1, 1, 1, 1, invert: true);
        _client.Packets.Clear();
        _client.Sent.Clear();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        Assert.Equal([102u], Removed());
        var area = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.AreaHideComposer).Body;
        Assert.Equal(1, area[^1]);
    }

    // Observed on AIR WIN63-202610201032-107964540: changing the rectangle while the hider is on moves the hidden furni and tiles but
    // sends no AreaHide.
    [Fact]
    public async Task ChangingTheRectangleWhileOnSendsNoAreaHide()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        PlaceNormal(102, 3, 3);
        await SaveAreaHide(100, 1, 1, 1, 1);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        _client.Packets.Clear();
        _client.Sent.Clear();

        await SaveAreaHide(100, 3, 3, 1, 1);

        Assert.DoesNotContain(ServerPacketHeader.AreaHideComposer, _client.Sent);
        Assert.Equal([102u], Removed());
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectsComposer);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HeightMapUpdateComposer);
    }

    // Observed on AIR WIN63-202610201032-107964540: a furni placed inside a hidden rectangle is accepted without an ObjectAdd.
    [Fact]
    public async Task AFurniPlacedInsideTheHiddenRectangleGetsNoObjectAdd()
    {
        var hider = PlaceAreaHider(100);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        _client.Packets.Clear();
        _client.Sent.Clear();

        PlaceNormal(101, 1, 1);

        Assert.NotNull(_room.GetRoomItemHandler().GetItem(101));
        Assert.DoesNotContain(ServerPacketHeader.ObjectAddComposer, _client.Sent);
    }

    // Observed on AIR WIN63-202610201032-107964540: the room entry carries a hider that is on in the FloorHeightMap list and sends no
    // separate AreaHide; the hidden furni are not in the entry.
    [Fact]
    public async Task TheRoomEntryCarriesTheHiderInTheFloorHeightMapAndHidesItsFurni()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);

        _client.Sent.Clear();
        _client.Packets.Clear();
        _room.SendObjects(_client);

        Assert.DoesNotContain(ServerPacketHeader.AreaHideComposer, _client.Sent);
        var map = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.FloorHeightMapComposer).Body;
        var entry = new byte[] { 0, 0, 0, 100, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 2, 0, 0, 0, 2, 0 };
        Assert.True(map.AsSpan().IndexOf(entry) >= 0, "the FloorHeightMap list holds no area hide entry");
        Assert.DoesNotContain(_room.VisibleFloorItems, item => item.Id == 101);
    }
}
