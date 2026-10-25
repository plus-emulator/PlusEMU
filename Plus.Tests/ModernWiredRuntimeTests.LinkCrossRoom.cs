using System.Diagnostics;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>A link event sent to a loaded room runs that room's stack at once, without a pass driven by the test.</summary>
public partial class ModernWiredRuntimeTests
{
    private static (TeleportFixture F, Item Sender, Item Receiver) CrossRoom(uint roomId, uint firstItem, CrossRoomLinkStore store, string text)
    {
        var f = new TeleportFixture();
        f.Room.Id = roomId;
        f.Room.OwnerId = 5;
        Assert.True(f.Engine.Remove(100));
        f.Room.GetWired().LinkStore = store;
        var sender = LinkFurni(f, firstItem, 0, 1);
        var receiver = LinkFurni(f, firstItem + 1, 0, 2);
        store.Join(roomId, sender.Id, 7);
        store.Join(roomId, receiver.Id, 7);
        LinkTextSendMessage(f, sender, 1, text);
        LinkTextReceiveMessage(f, receiver);
        LinkTextLog(f, "got [$(trigger_text)]");

        return (f, sender, receiver);
    }

    private static void LoadRooms(params TeleportFixture[] fixtures)
    {
        var loaded = fixtures.Select(fixture => fixture.Room).ToArray();
        var manager = DispatchProxyFor(loaded);

        foreach (var fixture in fixtures) {
            typeof(Room).GetField("_rooms", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(fixture.Room, manager);
            fixture.Isolation = new WakeIsolation(fixture.Room.GetWired());
        }
    }

    /// <summary>Gives a room its own wake threads and clock, so wake-ups armed by other tests cannot delay its passes.</summary>
    private sealed class WakeIsolation : IDisposable
    {
        private readonly WiredWakeWorkers _workers = new(2);
        private readonly WiredWakeClock _clock = new();

        public WakeIsolation(WiredComponent wired)
        {
            wired.Workers = _workers;
            wired.WakeAfter = _clock.After;
        }

        public void Dispose()
        {
            _clock.Dispose();
            _workers.Dispose();
        }
    }

    private static IRoomManager DispatchProxyFor(Room[] loaded)
    {
        var manager = System.Reflection.DispatchProxy.Create<IRoomManager, RecordingProxy>();
        ((RecordingProxy)(object)manager).InvokeMethod = (method, args) =>
        {
            Assert.Equal("TryGetRoom", method.Name);
            var room = loaded.FirstOrDefault(candidate => candidate.Id == (uint)args![0]!);
            args![1] = room;

            return room != null;
        };

        return manager;
    }

    private static bool WaitFor(Func<bool> condition, int milliseconds = 3000)
    {
        var clock = Stopwatch.StartNew();

        while (!condition()) {
            if (clock.ElapsedMilliseconds > milliseconds) {
                return false;
            }

            Thread.Sleep(1);
        }

        return true;
    }

    [Fact]
    public void ALinkMessageToAnotherLoadedRoomRunsItsStackWithoutWaitingForAPass()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "unused");
        using var _b = b;
        LoadRooms(a, b);

        a.Fire(land: false);

        Assert.True(WaitFor(() => LinkTextLogs(b).Length > 0), "The other room did not run its stack without a pass.");
        Assert.Equal(["got [hello]"], LinkTextLogs(b));
    }

    [Fact]
    public void ALinkMessageToARoomThatIsNotLoadedReachesNothing()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "hello");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "unused");
        using var _b = b;
        LoadRooms(a);

        a.Fire(land: false);
        b.Advance(100);

        Assert.False(WaitFor(() => LinkTextLogs(b).Length > 0, 300));
    }

    [Fact]
    public void TwoRoomsSendingToEachOtherEachRunTheOtherRoomsStack()
    {
        var store = new CrossRoomLinkStore();
        var (a, _, _) = CrossRoom(1, 50, store, "fromA");
        using var _a = a;
        var (b, _, _) = CrossRoom(2, 60, store, "fromB");
        using var _b = b;
        LoadRooms(a, b);

        for (var round = 0; round < 20; round++) {
            using var start = new Barrier(2);
            var senders = new[] { a, b }.Select(room => Task.Run(() =>
            {
                start.SignalAndWait();
                room.Fire(land: false);
            })).ToArray();

            Assert.True(Task.WaitAll(senders, 10000), "Two rooms sending to each other waited on each other.");
        }

        Assert.True(WaitFor(() => LinkTextLogs(a).Length >= 20 && LinkTextLogs(b).Length >= 20));
        Assert.All(LinkTextLogs(a), log => Assert.Equal("got [fromB]", log));
        Assert.All(LinkTextLogs(b), log => Assert.Equal("got [fromA]", log));
    }

    private sealed class CrossRoomLinkStore : IWiredLinkStore
    {
        private readonly Dictionary<uint, (uint Room, uint Network)> _endpoints = [];

        public void Join(uint roomId, uint itemId, uint networkId) => _endpoints[itemId] = (roomId, networkId);

        public WiredLinkEndpoint? Endpoint(uint itemId) => _endpoints.TryGetValue(itemId, out var joined) ? new(itemId, joined.Network, false) : null;

        public IReadOnlyList<WiredLinkConnection> Members(uint networkId) =>
            [.. _endpoints.Where(pair => pair.Value.Network == networkId).Select(pair => new WiredLinkConnection(pair.Value.Room, "", pair.Key, false))];

        public IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId) => throw new NotSupportedException();

        public IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId) => throw new NotSupportedException();

        public WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal) => throw new NotSupportedException();

        public WiredLinkResult Create(int ownerId, string name, out uint networkId) => throw new NotSupportedException();

        public WiredLinkResult Rename(int ownerId, uint networkId, string name) => throw new NotSupportedException();

        public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached) => throw new NotSupportedException();
    }
}
