using System.Data;
using Microsoft.Extensions.Time.Testing;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// WiredRoomUpdate (817: bool rollback). Reload sends everyone out of the room and back in, with the furni as they are now.
// Rollback does the same, but first puts every furni back to its place and state from when the room was loaded.
// Reload needs Wired modify rights; rollback needs the room owner or staff.
public partial class PlacedFurniRoomTests
{
    private const string LoadedWall = ":w=0,2 l=11,53 l";
    private const string PlacedWall = ":w=1,1 l=2,10 l";

    [Fact]
    public async Task ReloadSendsEveryoneOutAndBackWithTheFurniAsTheyAreNow()
    {
        var (store, unloads) = LoadedRoom();
        var (_, viewer) = InRoom(_client);
        var floor = _room.GetRoomItemHandler().GetItem(50);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, floor, 3, 2, 2, false, false, false));

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming(false));

        Assert.Equal(1, unloads.Count);
        Assert.Contains(store.Saved, save => save is { Id: 50, X: 3, Y: 2, Rotation: 2 });
        Assert.Empty(store.Restored);
        AssertSentOutAndBack(viewer);
        Assert.Null(_client.GetHabbo().CurrentRoom);
    }

    [Fact]
    public async Task RollbackPutsTheFurniBackToTheirLoadedOrPlacedPlaceAndStateAfterTheUnloadSave()
    {
        var (store, unloads) = LoadedRoom();
        var (_, viewer) = InRoom(_client);
        var handler = _room.GetRoomItemHandler();
        var floor = handler.GetItem(50);
        Assert.True(handler.SetFloorItem(null!, floor, 3, 2, 2, false, false, false));
        floor.LegacyDataString = "1";
        handler.GetItem(51).WallCoordinates = ":w=1,3 l=4,20 r";
        var placedLater = Furni(52, InteractionType.None, WiredBoxType.None);
        Assert.True(handler.SetFloorItem(null!, placedLater, 0, 0, 0, true, false, false));
        Assert.True(handler.SetFloorItem(null!, placedLater, 2, 2, 2, false, false, false));

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming(true));

        Assert.Equal(["save", "restore"], store.Steps);
        Assert.Equal(RoomId, store.RestoredRoom);
        var restoredFloor = Assert.Single(store.Restored, save => save.Id == 50);
        Assert.Equal((1, 1, 0.0, 0, "0"), (restoredFloor.X, restoredFloor.Y, restoredFloor.Z, restoredFloor.Rotation, restoredFloor.ExtraData));
        var restoredWall = Assert.Single(store.Restored, save => save.Id == 51);
        Assert.True(restoredWall.SaveWallPosition);
        Assert.Equal(LoadedWall, restoredWall.WallPosition);
        var restoredLater = Assert.Single(store.Restored, save => save.Id == 52);
        Assert.Equal((0, 0, 0.0, 0), (restoredLater.X, restoredLater.Y, restoredLater.Z, restoredLater.Rotation));
        AssertSentOutAndBack(viewer);
    }

    [Fact]
    public async Task RollbackKeepsTheScoresOfAHighscoreBoardButStillPutsItBack()
    {
        var (store, unloads) = LoadedRoom();
        InRoom(_client);
        var handler = _room.GetRoomItemHandler();
        var board = handler.GetItem(53);
        Assert.IsType<HighscoreDataFormat>(board.ExtraData).Entries = [new HighscoreEntry(9, ["owner"])];

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming(true));

        var restored = Assert.Single(store.Restored, save => save.Id == 53);
        Assert.Null(restored.ExtraData);
        Assert.Equal((3, 3), (restored.X, restored.Y));
    }

    [Fact]
    public async Task AWallFurniPlacedAfterTheLoadAndThenMovedGoesBackToItsPlacementSpot()
    {
        var (store, unloads) = LoadedRoom();
        InRoom(_client);
        var handler = _room.GetRoomItemHandler();
        var placedLater = Furni(54, InteractionType.None, WiredBoxType.None, ItemType.Wall);
        placedLater.WallCoordinates = PlacedWall;
        Assert.True(handler.SetWallItem(_client, placedLater));
        placedLater.WallCoordinates = ":w=3,5 l=6,30 r";

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming(true));

        var restored = Assert.Single(store.Restored, save => save.Id == 54);
        Assert.True(restored.SaveWallPosition);
        Assert.Equal(PlacedWall, restored.WallPosition);
    }

    [Fact]
    public async Task UsersWithModifyRightsCanReloadButOnlyTheOwnerCanRollBack()
    {
        var (store, unloads) = LoadedRoom();
        var builder = new TestClient();
        builder.SetHabbo(new Habbo { Id = 8, Username = "builder", CurrentRoom = _room, Access = UserAccess.Empty });
        _room.UsersWithRights = [8];
        Assert.True(_room.GetWired().Settings.TrySave(_client, 2, 2, "", out _));

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, builder, EditorTestSupport.Incoming(true));
        Assert.Empty(unloads);
        Assert.Empty(builder.Sent);

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, builder, EditorTestSupport.Incoming(false));
        Assert.Single(unloads);
        Assert.Empty(store.Restored);
    }

    [Fact]
    public async Task VisitorsWithoutModifyRightsCannotReloadOrRollBack()
    {
        var (_, unloads) = LoadedRoom();
        var visitor = new TestClient();
        visitor.SetHabbo(new Habbo { Id = 9, Username = "visitor", CurrentRoom = _room, Access = UserAccess.Empty });
        _room.UsersWithRights = [];

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, visitor, EditorTestSupport.Incoming(false));
        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, visitor, EditorTestSupport.Incoming(true));

        Assert.Empty(unloads);
        Assert.Empty(visitor.Sent);
    }

    [Fact]
    public async Task AFrameWithoutExactlyOneBoolIsDropped()
    {
        var (_, unloads) = LoadedRoom();

        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming());
        await new WiredRoomUpdateEvent(RoomUpdates(unloads)).Parse(_room, _client, EditorTestSupport.Incoming(false, false));

        Assert.Empty(unloads);
    }

    [Fact]
    public void WiredRoomUpdateIsReadFromHeader817()
    {
        var revision = new Plus.Communication.Revisions.RevisionsCache().InternalRevision;

        Assert.Equal(817u, Plus.Communication.Packets.Incoming.ClientPacketHeader.WiredRoomUpdateEvent);
        Assert.Equal(Plus.Communication.Packets.Incoming.ClientPacketHeader.WiredRoomUpdateEvent, revision.IncomingIdToInternalIdMapping[817]);
        Assert.Single(revision.IncomingHeaders, entry => entry.Value == 817);
    }

    private (RoomStateStore Store, List<uint> Unloads) LoadedRoom()
    {
        var store = new RoomStateStore();
        Set("_roomItemHandling", new RoomItemHandling(_room, store, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));
        _room.UsersWithRights ??= [];
        var table = new DataTable();

        foreach (var column in new[] { "id", "user_id", "x", "y", "rot", "limited_number", "limited_stack", "base_item" }) {
            table.Columns.Add(column, typeof(int));
        }

        table.Columns.Add("z", typeof(double));

        foreach (var column in new[] { "extra_data", "wall_pos", "username" }) {
            table.Columns.Add(column, typeof(string));
        }

        var floor = Furni(50, InteractionType.None, WiredBoxType.None).Definition;
        floor.Modes = 2;
        var wall = Furni(51, InteractionType.None, WiredBoxType.None, ItemType.Wall).Definition;
        var board = Furni(53, InteractionType.None, WiredBoxType.None).Definition;
        board.ItemName = "highscore_classic";
        _room.GetRoomItemHandler().LoadFurniture([
            ItemLoader.ReadRoomItem(table.Rows.Add(50, 7, 1, 1, 0, 0, 0, 50, 0.0, "0", "", "owner"), RoomId, floor),
            ItemLoader.ReadRoomItem(table.Rows.Add(51, 7, 0, 0, 0, 0, 0, 51, 0.0, "", LoadedWall, "owner"), RoomId, wall),
            ItemLoader.ReadRoomItem(table.Rows.Add(53, 7, 3, 3, 0, 0, 0, 53, 0.0, new HighscoreDataFormat { State = "0", ScoreType = 2 }.Serialize(), "", "owner"), RoomId, board)
        ]);
        _room.GetGameMap().GenerateMaps();

        return (store, []);
    }

    private (RoomUser User, TestClient Client) InRoom(TestClient client)
    {
        var user = Viewer(3, 3);
        client.Sent.Clear();
        client.Packets.Clear();

        return (user, client);
    }

    private readonly FakeTimeProvider _roomClock = new();

    private IWiredRoomStateService RoomUpdates(List<uint> unloads) => new WiredRoomStateService(Proxy<IRoomManager>((method, args) =>
    {
        Assert.Equal("UnloadRoom", method);
        unloads.Add((uint)args[0]!);
        _room.GetRoomItemHandler().Dispose();

        return null;
    }), _roomClock);

    private void AssertSentOutAndBack(TestClient client)
    {
        _roomClock.Advance(TimeSpan.FromMilliseconds(1999));
        Assert.DoesNotContain(client.Packets, packet => packet.Header == ServerPacketHeader.RoomForwardComposer);
        _roomClock.Advance(TimeSpan.FromMilliseconds(1));

        var sent = client.Packets.Select(packet => (packet.Header, Convert.ToHexString(packet.Body))).ToArray();

        Assert.Equal(
            [
                (ServerPacketHeader.YouArePlayingGameComposer, "00"),
                (ServerPacketHeader.CloseConnectionComposer, "0002"),
                (ServerPacketHeader.RoomForwardComposer, RoomId.ToString("X8"))
            ],
            sent.Where(packet => packet.Header is ServerPacketHeader.YouArePlayingGameComposer or ServerPacketHeader.CloseConnectionComposer
                or ServerPacketHeader.RoomForwardComposer).ToArray());
        Assert.Equal(ServerPacketHeader.RoomForwardComposer, sent[^1].Header);
    }

    private sealed class RoomStateStore : IRoomItemStore
    {
        public List<string> Steps { get; } = [];
        public List<RoomItemSave> Saved { get; } = [];
        public List<RoomItemSave> Restored { get; } = [];
        public uint? RestoredRoom { get; private set; }
        public void AssignOwner(uint itemId, int userId) { }
        public void ClearRoom(uint itemId) { }
        public void SaveWallPosition(uint itemId, string wallPosition) { }
        public void PlaceFloor(uint itemId, uint roomId, int x, int y, double z, int rotation) { }
        public void PlaceWall(uint itemId, uint roomId, int x, int y, double z, int rotation, string wallPosition) { }

        public void SaveMoved(IReadOnlyList<RoomItemSave> items)
        {
            Steps.Add("save");
            Saved.AddRange(items);
        }

        public void Restore(uint roomId, IReadOnlyList<RoomItemSave> items)
        {
            Steps.Add("restore");
            RestoredRoom = roomId;
            Restored.AddRange(items);
        }
    }
}
