using System.Buffers.Binary;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.AreaHide;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private static bool HoldsId(byte[] body, uint id)
    {
        var needle = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(needle, id);

        return body.AsSpan().IndexOf(needle) >= 0;
    }

    private IEnumerable<byte[]> Bodies(uint header) => _client.Packets.Where(packet => packet.Header == header).Select(packet => packet.Body);

    private Item PlaceWiredEffect(uint id, int x, int y)
    {
        var item = Furni(id, InteractionType.WiredEffect, WiredBoxType.EffectMatchPosition);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, x, y, 0, true, false, true));

        return item;
    }

    private Item PlaceStacked(uint id)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.ItemName = "colortile";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, (int)(id % 4), 3, 0, true, false, false));

        return item;
    }

    private void ClearPackets()
    {
        _client.Packets.Clear();
        _client.Sent.Clear();
    }

    // A room entry or a visibility check of N furni must not scan the room once per furni when no hider or controller is on.
    [Fact]
    public void TheRoomEntryScansForHidersOncePerCallNotOncePerFurni()
    {
        PlaceAreaHider(100);

        for (uint id = 200; id < 240; id++) {
            PlaceStacked(id);
        }

        var before = FurniVisibility.Scans;
        _ = _room.VisibleFloorItems.ToArray();
        Assert.True(FurniVisibility.Scans - before <= 1, $"VisibleFloorItems scanned {FurniVisibility.Scans - before} times for 41 furni");

        before = FurniVisibility.Scans;
        FurniVisibility.Seed(_room);
        Assert.True(FurniVisibility.Scans - before <= 1, $"Seed scanned {FurniVisibility.Scans - before} times for 41 furni");

        before = FurniVisibility.Scans;
        FurniVisibility.RefreshFurni(_room);
        Assert.True(FurniVisibility.Scans - before <= 1, $"RefreshFurni scanned {FurniVisibility.Scans - before} times for 41 furni");
    }

    // The room entry lists every floor item that no hider covers.
    [Fact]
    public async Task TheEntryStillHidesOnlyTheCoveredFurniWhenAHiderIsOn()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        PlaceNormal(102, 3, 3);
        await SaveAreaHide(100, 1, 1, 1, 1);
        hider.Interactor.OnTrigger(_client, hider, 0, true);

        Assert.Equal([100u, 102u], _room.VisibleFloorItems.Select(item => item.Id).Order().ToArray());
    }

    // Hiding the wired of a room is a room choice; the area hider must not undo it.
    [Fact]
    public async Task TurningTheAreaHiderOffKeepsTheHiddenWiredHidden()
    {
        var hider = PlaceAreaHider(100);
        PlaceNormal(101, 1, 1);
        PlaceWiredEffect(70001, 2, 2);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        _room.SetWiredHidden(true);
        ClearPackets();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        var added = Bodies(ServerPacketHeader.ObjectsComposer).ToList();
        Assert.Single(added);
        Assert.True(HoldsId(added[0], 101));
        Assert.False(HoldsId(added[0], 70001), "the hidden wired box was sent to the clients");
    }

    [Fact]
    public async Task ShowingTheWiredAgainDoesNotSendTheWiredInsideAHiddenArea()
    {
        var hider = PlaceAreaHider(100);
        PlaceWiredEffect(70001, 2, 2);
        PlaceWiredEffect(70002, 3, 0);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        _room.SetWiredHidden(true);
        ClearPackets();

        _room.SetWiredHidden(false);

        var added = Bodies(ServerPacketHeader.ObjectsComposer).ToList();
        Assert.Single(added);
        Assert.True(HoldsId(added[0], 70002));
        Assert.False(HoldsId(added[0], 70001), "the wired box inside the hidden area was sent");
    }

    // A furni that is inside a hidden area is not on the clients, wherever it came from.
    [Fact]
    public async Task AFurniMovedIntoAHiddenAreaIsRemovedFromTheClients()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 0, 3);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 2, 2, 0, false, false, true));

        Assert.Equal([101u], Removed());
        Assert.DoesNotContain(ServerPacketHeader.ObjectUpdateComposer, _client.Sent);
        Assert.True(FurniVisibility.IsConcealed(_room, item));
    }

    [Fact]
    public async Task AHiddenFurniMovedOutOfTheAreaIsAddedForTheClients()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 2, 2);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 0, 3, 0, false, false, true));

        var added = Assert.Single(Bodies(ServerPacketHeader.ObjectsComposer));
        Assert.True(HoldsId(added, 101));
        Assert.DoesNotContain(ServerPacketHeader.ObjectUpdateComposer, _client.Sent);
        Assert.False(FurniVisibility.IsConcealed(_room, item));
    }

    [Fact]
    public async Task AHiddenFurniMovedInsideTheAreaSendsNothing()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 1, 1);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 2, 2, 0, false, false, true));

        Assert.DoesNotContain(_client.Sent, id => id is ServerPacketHeader.ObjectUpdateComposer or ServerPacketHeader.ObjectsComposer
            or ServerPacketHeader.ObjectRemoveMultipleComposer);
    }

    [Fact]
    public async Task AFurniMovedOutsideTheAreaStillGetsItsObjectUpdate()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 0, 3);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_client, item, 1, 3, 0, false, false, true));

        Assert.Contains(ServerPacketHeader.ObjectUpdateComposer, _client.Sent);
        Assert.Empty(Removed());
    }

    [Fact]
    public async Task AWiredMoveIntoAHiddenAreaRemovesTheFurniWithoutAnUpdate()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 0, 3);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(WiredRoomOperations.MoveItem(_room, item, 1, 1, animate: false));

        Assert.Equal([101u], Removed());
        Assert.DoesNotContain(ServerPacketHeader.ObjectUpdateComposer, _client.Sent);
    }

    // Rollers and the in-place setter commit through the same step.
    [Fact]
    public async Task ARollerStyleMoveOutOfAHiddenAreaAddsTheFurni()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 1, 1);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(item, 3, 3, item.GetZ));

        Assert.True(HoldsId(Assert.Single(Bodies(ServerPacketHeader.ObjectsComposer)), 101));
        Assert.False(FurniVisibility.IsConcealed(_room, item));
    }

    [Fact]
    public async Task ARollerStyleMoveIntoAHiddenAreaRemovesTheFurni()
    {
        var hider = PlaceAreaHider(100);
        var item = PlaceNormal(101, 3, 3);
        await SaveAreaHide(100, 1, 1, 2, 2);
        hider.Interactor.OnTrigger(_client, hider, 0, true);
        ClearPackets();

        Assert.True(_room.GetRoomItemHandler().SetFloorItem(item, 1, 1, item.GetZ));

        Assert.Equal([101u], Removed());
    }

    // The entry height map marks the tiles of a hider that is on as 0xFFFF.
    [Fact]
    public void TheEntryHeightMapMarksTheHiddenTiles()
    {
        PlaceAreaHider(100, "1\n1\n1\n2\n2\n0\n1\n0");
        ClearPackets();

        _room.SendObjects(_client);

        var map = Assert.Single(Bodies(ServerPacketHeader.HeightMapComposer));
        var width = BinaryPrimitives.ReadInt32BigEndian(map);
        Assert.Equal(4, width);
        short Tile(int x, int y) => BinaryPrimitives.ReadInt16BigEndian(map.AsSpan(8 + (y * width + x) * 2));
        var hidden = Enumerable.Range(0, 4).SelectMany(y => Enumerable.Range(0, 4).Select(x => (x, y))).Where(tile => Tile(tile.x, tile.y) == -1).ToList();
        Assert.Equal([(1, 1), (2, 1), (1, 2), (2, 2)], hidden);
    }

    [Fact]
    public void TheEntryHeightMapHidesNothingWithoutAHider()
    {
        PlaceAreaHider(100, "0\n1\n1\n2\n2\n0\n1\n0");
        ClearPackets();

        _room.SendObjects(_client);

        var map = Assert.Single(Bodies(ServerPacketHeader.HeightMapComposer));
        Assert.DoesNotContain(Enumerable.Range(0, 16), i => BinaryPrimitives.ReadInt16BigEndian(map.AsSpan(8 + i * 2)) == -1);
    }

    // The tiles told at the entry are the tiles the next turn-off gives back.
    [Fact]
    public void TurningAHiderOffAfterTheEntryGivesTheHiddenTilesBack()
    {
        var hider = PlaceAreaHider(100, "1\n1\n1\n2\n2\n0\n1\n0");
        _room.SendObjects(_client);
        ClearPackets();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        var tiles = Assert.Single(Bodies(ServerPacketHeader.HeightMapUpdateComposer));
        Assert.Equal(4, tiles[0]);
        Assert.All(Enumerable.Range(0, 4), i => Assert.NotEqual((short)-1, BinaryPrimitives.ReadInt16BigEndian(tiles.AsSpan(1 + i * 4 + 2))));
    }
}
