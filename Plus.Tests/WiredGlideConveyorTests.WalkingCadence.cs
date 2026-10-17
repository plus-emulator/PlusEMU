using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    [Theory]
    [InlineData(true, "v2")]
    [InlineData(false, "v2")]
    [InlineData(true, "legacy")]
    [InlineData(false, "legacy")]
    public void WiredStepsAvoidSynchronousPositionWritesAndSaveTheirFinalPositions(bool reset, string engine)
    {
        var store = new MovementPositionStore();
        var layout = ColourTileFastQueueLayout().Where(item => reset || item.Id != 10).ToArray();
        var f = new Fixture(layout, live: true, movementEngine: engine, itemStore: store);
        f.Walker(1, 4, 10, 4, 11);
        store.Placements = 0;
        f.Advance(250);
        Assert.Equal(0, store.Placements);
        var handler = f.Room.GetRoomItemHandler();
        typeof(RoomItemHandling).GetMethod("SaveFurniture", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(handler, null);

        foreach (var id in Enumerable.Range(1, 7)) {
            var saved = Assert.Single(store.Saved.Where(item => item.Id == id));
            Assert.Equal((f.Items[(uint)id].GetX, f.Items[(uint)id].GetY, f.Items[(uint)id].GetZ), (saved.X, saved.Y, saved.Z));
            Assert.Equal(reset ? 3 + id : 4 + id, saved.X);
        }

        // Ordinary placement callers keep their immediate persistence contract.
        var tile = f.Items[7];
        Assert.True(handler.SetFloorItem(null!, tile, tile.GetX, tile.GetY, tile.Rotation, false, false, false, height: tile.GetZ));
        Assert.Equal(1, store.Placements);
    }

    [Fact]
    public async Task DueWalkingTickRetriesBeforeNextWiredPassWhenBusyRoomBecomesFree()
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        rider.MoveTo(4, 10);
        var room = f.Room;
        typeof(Room).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(room, Array.Empty<IRoomComponent>());
        var clock = new WalkingCycleClock();
        var manager = (RoomManager)RuntimeHelpers.GetUninitializedObject(typeof(RoomManager));
        void Set(string field, object value) => typeof(RoomManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);
        Set("_clock", clock);
        Set("_rooms", new ConcurrentDictionary<uint, Room>([new(room.Id, room)]));
        Set("_fastWiredRooms", new ConcurrentDictionary<uint, Room>([new(room.Id, room)]));
        Set("_pendingFullRooms", new ConcurrentDictionary<uint, Room>());
        Set("_cycleLastExecution", clock.Now);
        Set("_wiredLastExecution", clock.Now);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(RoomCycle.TryStart(room, () => { started.SetResult(); release.Task.GetAwaiter().GetResult(); }));
        var busy = room.ProcessTask;

        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            clock.Now += TimeSpan.FromMilliseconds(500);
            manager.OnCycle();
            Assert.Same(busy, room.ProcessTask);
            Assert.Equal(0, room.IsLagging); // A wired pass holding the slot defers the tick without counting a miss.
        }
        finally {
            release.SetResult();
        }

        await busy.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Now += TimeSpan.FromMilliseconds(25);
        manager.OnCycle();
        Assert.NotSame(busy, room.ProcessTask); // Both periodic deadlines are still in the future.
        await room.ProcessTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, room.IsLagging);
        Assert.False(room.IsCrashed);
        Assert.True(rider.IsWalking);
        var completed = room.ProcessTask;
        manager.OnCycle();
        Assert.Same(completed, room.ProcessTask); // One missed tick produces exactly one retry.
    }

    private sealed class WalkingCycleClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    private sealed class MovementPositionStore : IRoomItemStore
    {
        public int Placements;
        public List<RoomItemSave> Saved = [];
        public void AssignOwner(uint itemId, int userId) { }
        public void ClearRoom(uint itemId) { }
        public void SaveWallPosition(uint itemId, string wallPosition) { }
        public void SaveMoved(IReadOnlyList<RoomItemSave> items) => Saved.AddRange(items);
        public void PlaceFloor(uint itemId, uint roomId, int x, int y, double z, int rotation) => Placements++;
        public void PlaceWall(uint itemId, uint roomId, int x, int y, double z, int rotation, string wallPosition) { }
    }
}
