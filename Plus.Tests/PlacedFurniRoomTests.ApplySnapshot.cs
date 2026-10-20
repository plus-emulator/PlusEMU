using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// class_3027 [box id] (AIR WIN63-202610131318-860371703): "Apply Furni to set conditions" restores the box's saved snapshot.
public partial class PlacedFurniRoomTests
{
    private const string ApplyWallBaseline = ":w=1,2 l=11,53 l";
    private const string ApplyWallMoved = ":w=2,1 l=20,80 l";

    [Fact]
    public void ApplySnapshotRestoresStateRotationPositionAndWall()
    {
        var (floor, wall, store) = ApplySnapshotRoom(out _);
        var original = WiredRoomOperations.Capture(floor);
        ApplySnapshotDisturb(floor, wall);
        _client.Packets.Clear();

        Assert.True(_room.GetWired().ApplySnapshot(_client, 400));

        Assert.Equal(original, WiredRoomOperations.Capture(floor));
        Assert.Equal(ApplyWallBaseline, wall.WallCoordinates);
        Assert.Equal((wall.Id, ApplyWallBaseline), Assert.Single(store.Writes));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemUpdateComposer);
        // The snapshot was not re-captured: a second apply finds nothing to change.
        Assert.False(_room.GetWired().ApplySnapshot(_client, 400));
    }

    // A snapshot saved while a switch still held "" restores it to "0", the state such furni now load with.
    [Fact]
    public void ApplySnapshotRestoresAnEmptySavedStateAsZero()
    {
        var (floor, wall, _) = ApplySnapshotRoom(out _, floorState: "");
        ApplySnapshotDisturb(floor, wall);

        Assert.True(_room.GetWired().ApplySnapshot(_client, 400));

        Assert.Equal("0", floor.LegacyDataString);
    }

    [Fact]
    public async Task ApplySnapshotEventDecodesTheBoxIdAndDelegates()
    {
        var (floor, wall, _) = ApplySnapshotRoom(out _);
        var original = WiredRoomOperations.Capture(floor);
        ApplySnapshotDisturb(floor, wall);
        var handler = new ApplySnapshotEvent(new WiredRequestGateService(new ApplySnapshotFrozenClock()));

        await handler.Parse(_room, _client, ClientPacket(400, 0));
        Assert.Equal(("1", 3, 2, 2), (floor.LegacyDataString, floor.GetX, floor.GetY, floor.Rotation)); // Trailing data is a malformed packet.

        await handler.Parse(_room, _client, ClientPacket(400));
        Assert.Equal(original, WiredRoomOperations.Capture(floor));
        Assert.Equal(ApplyWallBaseline, wall.WallCoordinates);

        // A repeat inside the request gate's interval is dropped.
        ApplySnapshotDisturb(floor, wall);
        await handler.Parse(_room, _client, ClientPacket(400));
        Assert.Equal(("1", 3, 2, 2), (floor.LegacyDataString, floor.GetX, floor.GetY, floor.Rotation));
    }

    [Fact]
    public void ApplySnapshotNeedsWiredWritePermission()
    {
        var (floor, wall, store) = ApplySnapshotRoom(out _);
        ApplySnapshotDisturb(floor, wall);
        var moved = WiredRoomOperations.Capture(floor);
        var visitor = new TestClient();
        visitor.SetHabbo(new Habbo { Id = 9, Username = "visitor", CurrentRoom = _room, Access = UserAccess.Empty });

        Assert.False(_room.GetWired().ApplySnapshot(visitor, 400));
        // The owner outside the room cannot apply either.
        _client.GetHabbo().CurrentRoom = null;
        Assert.False(_room.GetWired().ApplySnapshot(_client, 400));

        Assert.Equal(moved, WiredRoomOperations.Capture(floor));
        Assert.Equal(ApplyWallMoved, wall.WallCoordinates);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void ApplySnapshotIgnoresUnknownBoxesAndBoxesWithoutSnapshots()
    {
        var (floor, wall, store) = ApplySnapshotRoom(out _);
        ApplySnapshotDisturb(floor, wall);
        var moved = WiredRoomOperations.Capture(floor);
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        var emptyItem = Furni(401, InteractionType.WiredEffect, WiredBoxType.EffectMatchPosition);
        emptyItem.RoomId = RoomId;
        var empty = Assert.IsType<WiredModernAction>(_room.GetWired().CreateConfiguredBox(emptyItem, descriptor));
        Assert.True(_room.GetWired().AddBox(empty));
        Assert.Empty(empty.Configuration.Snapshots);

        Assert.False(_room.GetWired().ApplySnapshot(_client, 999));
        Assert.False(_room.GetWired().ApplySnapshot(_client, 401));
        Assert.False(_room.GetWired().ApplySnapshot(_client, floor.Id));

        Assert.Equal(moved, WiredRoomOperations.Capture(floor));
        Assert.Equal(ApplyWallMoved, wall.WallCoordinates);
        Assert.Empty(store.Writes);
    }

    [Fact]
    public void ApplySnapshotSkipsRemovedFurniAndBlockedTiles()
    {
        var (floor, wall, _) = ApplySnapshotRoom(out var action);
        _room.GetRoomItemHandler().RemoveFurniture(null!, wall.Id);
        var blocker = Furni(303, InteractionType.None, WiredBoxType.None);
        blocker.Definition.Stackable = false;
        blocker.RoomId = RoomId;
        Assert.True(WiredRoomOperations.MoveItem(_room, floor, 3, 3, 2, animate: false));
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, blocker, 1, 1, 0, true, false, false));
        floor.LegacyDataString = "1";

        // The state still returns; the blocked move is skipped and the removed wall item is ignored.
        Assert.True(_room.GetWired().ApplySnapshot(_client, action.Item.Id));
        Assert.Equal(("0", 3, 3), (floor.LegacyDataString, floor.GetX, floor.GetY));
    }

    private (Item Floor, Item Wall, WallSnapshotStore Store) ApplySnapshotRoom(out WiredModernAction action, string floorState = "0")
    {
        var store = WallSnapshotInstallStore();
        var wall = WallSnapshotItem(301, ApplyWallBaseline);
        var floor = Furni(302, InteractionType.None, WiredBoxType.None);
        floor.Definition.Modes = 2;
        floor.RoomId = RoomId;
        floor.ExtraData = new LegacyDataFormat { Data = floorState };
        _room.GetRoomItemHandler().LoadFurniture([wall]);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, floor, 1, 1, 0, true, false, false));
        action = WallSnapshotAction();
        Assert.True(WiredNativeTestSupport.TrySavePrepared(action, new() { IntParams = [1, 1, 1, 1, 100], SelectedItems = [wall.Id, floor.Id] },
            TestWiredConfigurationStore.Instance, out var error), error);
        Assert.Equal(2, action.Configuration.Snapshots.Length);
        Assert.True(_room.GetWired().AddBox(action));
        var actor = new RoomUser(7, RoomId, 1, _room, _client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        BuiltinUsers()[actor.VirtualId] = actor;

        return (floor, wall, store);
    }

    private void ApplySnapshotDisturb(Item floor, Item wall)
    {
        Assert.True(WiredRoomOperations.MoveItem(_room, floor, 3, 2, 2, animate: false));
        floor.LegacyDataString = "1";
        wall.WallCoordinates = ApplyWallMoved;
    }

    // The monotonic clock never advances, so only the first request passes the gate.
    private sealed class ApplySnapshotFrozenClock : TimeProvider
    {
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => 0;
    }
}
