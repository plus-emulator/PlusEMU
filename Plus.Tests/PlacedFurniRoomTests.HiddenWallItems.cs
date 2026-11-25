using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// With "Hide wall items" on, an area hider that is on also hides the wall items whose front tile it hides, just like Habbo.
// The front tile of a back wall item (r) at w=x,y is (x, y + 1); of a left wall item (l) it is (x + 1, y).
// Wall items go with ItemRemoveMultiple (picker -1, flag on) and come back with one Items packet.
public partial class PlacedFurniRoomTests
{
    private Item PlaceWall(uint id, string position)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None, ItemType.Wall);
        item.WallCoordinates = position;
        Assert.True(_room.GetRoomItemHandler().SetWallItem(_client, item));

        return item;
    }

    private List<uint> RemovedWalls() => _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ItemRemoveMultipleComposer)
        .SelectMany(packet => RemovedIds(packet.Body)).ToList();

    private async Task<Item> HiderOn(int x, int y, int w, int l, bool wallItems = true, bool invert = false)
    {
        var hider = PlaceAreaHider(100);
        await SaveAreaHide(100, x, y, w, l, wallItems: wallItems, invert: invert);
        hider.Interactor.OnTrigger(_client, hider, 0, true);

        return hider;
    }

    [Theory]
    [InlineData(":w=1,0 l=10,30 r", 1, 1, 1, 1, true)]
    [InlineData(":w=1,0 l=10,30 r a=247", 1, 1, 1, 1, true)]
    [InlineData(":w=1,0 l=10,30 r", 1, 2, 1, 2, false)]
    [InlineData(":w=2,0 l=10,30 r", 1, 1, 1, 3, false)]
    [InlineData(":w=0,2 l=10,30 l", 1, 2, 1, 1, true)]
    [InlineData(":w=0,2 l=10,30 l", 2, 2, 1, 1, false)]
    [InlineData(":w=0,2 l=10,30 l", 1, 3, 1, 1, false)]
    public async Task AWallItemIsHiddenWhenTheHiderHidesItsFrontTile(string position, int x, int y, int w, int l, bool hidden)
    {
        var wall = PlaceWall(30, position);

        await HiderOn(x, y, w, l);

        Assert.Equal(hidden, FurniVisibility.ShouldHide(_room, wall));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TurningTheHiderOnRemovesTheWallItemsOnlyWithTheWallItemsOption(bool wallItems)
    {
        PlaceWall(30, ":w=1,0 l=10,30 r");
        PlaceNormal(101, 1, 1);
        _client.Packets.Clear();

        await HiderOn(1, 1, 2, 2, wallItems);

        Assert.Equal([101u], Removed());
        Assert.Equal(wallItems ? [30u] : [], RemovedWalls());
        Assert.DoesNotContain(ServerPacketHeader.ItemRemoveComposer, _client.Sent);
    }

    [Theory]
    [InlineData(":w=3,0 l=10,30 r", true)]
    [InlineData(":w=1,0 l=10,30 r", false)]
    public async Task AnInvertedHiderHidesTheWallItemsOutsideTheRectangle(string position, bool hidden)
    {
        PlaceWall(30, position);
        _client.Packets.Clear();

        await HiderOn(1, 1, 1, 3, invert: true);

        Assert.Equal(hidden ? [30u] : [], RemovedWalls());
    }

    [Fact]
    public async Task TurningTheHiderOffAddsTheWallItemsAgainWithOneItemsPacket()
    {
        PlaceWall(30, ":w=1,0 l=10,30 r");
        var hider = await HiderOn(1, 1, 2, 2);
        _client.Packets.Clear();
        _client.Sent.Clear();

        hider.Interactor.OnTrigger(_client, hider, 0, true);

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemsComposer);
        Assert.False(FurniVisibility.IsConcealed(_room, _room.GetRoomItemHandler().GetItem(30)!));
    }

    [Fact]
    public async Task TheRoomEntryLeavesOutTheHiddenWallItems()
    {
        PlaceWall(30, ":w=1,0 l=10,30 r");
        PlaceWall(31, ":w=3,0 l=10,30 r");

        await HiderOn(1, 1, 2, 2);

        Assert.Equal([31u], _room.VisibleWallItems.Select(item => item.Id).ToList());
    }

    [Fact]
    public async Task AWallItemPlacedInFrontOfTheHiddenAreaGetsNoItemAdd()
    {
        await HiderOn(1, 1, 2, 2);
        _client.Packets.Clear();
        _client.Sent.Clear();

        var wall = Furni(30, InteractionType.None, WiredBoxType.None, ItemType.Wall);
        wall.WallCoordinates = ":w=1,0 l=10,30 r";
        Assert.True(_room.GetRoomItemHandler().SetWallItem(_client, wall));
        _room.SendObject(wall);

        Assert.DoesNotContain(ServerPacketHeader.ItemAddComposer, _client.Sent);
    }

    [Fact]
    public async Task MovingAWallItemIntoTheHiddenPartRemovesItAndMovingItOutAddsIt()
    {
        InstallRoomItemStore(new WallMoveStore());
        var wall = PlaceWall(30, ":w=3,0 l=10,30 r");
        await HiderOn(1, 1, 2, 2);
        _client.Packets.Clear();
        _client.Sent.Clear();

        Assert.True(_room.GetRoomItemHandler().MoveWallItem(wall, ":w=1,0 l=10,30 r"));

        Assert.Contains(ServerPacketHeader.ItemRemoveComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.ItemUpdateComposer, _client.Sent);
        _client.Sent.Clear();

        Assert.True(_room.GetRoomItemHandler().MoveWallItem(wall, ":w=3,0 l=20,30 r"));

        Assert.Contains(ServerPacketHeader.ItemAddComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.ItemRemoveComposer, _client.Sent);
    }

    [Fact]
    public async Task PickingUpAHiddenWallItemSendsNoItemRemove()
    {
        var wall = PlaceWall(30, ":w=1,0 l=10,30 r");
        await HiderOn(1, 1, 2, 2);
        _client.Sent.Clear();

        Assert.True(_room.GetRoomItemHandler().RemoveFurniture(_client, wall));

        Assert.DoesNotContain(ServerPacketHeader.ItemRemoveComposer, _client.Sent);
    }

    private sealed class WallMoveStore : IRoomItemStore
    {
        public void AssignOwner(uint itemId, int userId) { }
        public void ClearRoom(uint itemId) { }
        public void SaveWallPosition(uint itemId, string wallPosition) { }
        public void MoveWall(uint itemId, uint roomId, string wallPosition) { }
        public void SaveMoved(IReadOnlyList<RoomItemSave> items) { }
        public void PlaceFloor(uint itemId, uint roomId, int x, int y, double z, int rotation) { }
        public void PlaceWall(uint itemId, uint roomId, int x, int y, double z, int rotation, string wallPosition) { }
    }
}
