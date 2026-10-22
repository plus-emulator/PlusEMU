using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// The real room manager runs the real room on a fake monotonic clock. Observed on AIR WIN63-202610201032-107964540: the Wired clock ticks
// every 50 ms on the room's 500 ms grid; on a room tick the UserUpdate comes first and the Wired output follows in the same ms.
public partial class PlacedFurniRoomTests
{
    private const uint GridWalkTile = 10, GridTarget = 20, GridWalkStack = 30, GridTimerStackId = 40;
    private const uint GridMoveTile = 50, GridMoveBox = 60, GridMoveTileStack = 70, GridMoveTarget = 80;

    [Fact]
    public void FastWiredPassesStartOncePerFiftyMsSlotOfTheRoomGrid()
    {
        var (clock, manager) = GridRoom();
        ExecutorActor(1, 0);
        clock.Ms += 13;

        // A game loop that wakes every 26 ms: its polls drift against the grid.
        var passes = RunRoom(manager, clock, clock.Ms + 3000, period: 26);

        var slots = passes.Select(pass => pass.Slot).ToArray();
        Assert.Equal(Enumerable.Range(0, slots.Length).Select(index => slots[0] + 50L * index), slots);
        Assert.All(passes, pass => Assert.Equal(pass.Slot % 500 == 0, pass.Full));
    }

    [Fact]
    public void AWalkOnStackSendsItsOutputInTheRoomTickThatAnnouncesTheStepAfterItsUserUpdate()
    {
        var (clock, manager) = GridRoom();
        var target = GridWalkOnStack(delay: 0);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2000, period: 25);

        var output = Assert.Single(passes, pass => Touches(pass, target));
        Assert.True(output.Full);
        Assert.Contains("mv 1,1,", StatusOf(output, actor));
        AssertUserUpdateFirst(output, target);
    }

    [Fact]
    public void AWalkOnDelayOfOneLandsInTheRoomTickHalfASecondLaterAfterItsUserUpdate()
    {
        var (clock, manager) = GridRoom();
        var target = GridWalkOnStack(delay: 1);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);

        var step = Assert.Single(passes, pass => pass.Full && HasUserUpdate(pass) && StatusOf(pass, actor).Contains("mv 1,1,"));
        var output = Assert.Single(passes, pass => Touches(pass, target));
        Assert.Equal(step.At + 500, output.At);
        Assert.True(output.Full);
        AssertUserUpdateFirst(output, target);
    }

    [Fact]
    public void ARepeatEveryStackSendsItsOutputInTheRoomTickAfterItsUserUpdate()
    {
        var (clock, manager) = GridRoom();
        var target = GridTimerStack("wf_trg_periodically", 1);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2000, period: 25);

        var outputs = passes.Where(pass => Touches(pass, target)).ToArray();
        Assert.True(outputs.Length >= 3, $"{outputs.Length} outputs");
        Assert.All(outputs, pass => Assert.True(pass.Full, $"output at {pass.At} in a fast pass"));
        Assert.Contains(outputs, HasUserUpdate);
        Assert.All(outputs.Where(HasUserUpdate), pass => AssertUserUpdateFirst(pass, target));
    }

    [Fact]
    public void ARepeatShortOf200MsFiresOnTheWiredGridWithoutDriftOrLostTicksFor10Seconds()
    {
        var (clock, manager) = GridRoom();
        var target = GridTimerStack("wf_trg_period_short", 4);
        ExecutorActor(1, 0);
        clock.Ms += 13;

        // Late polls: each pass starts up to 25 ms after its 50 ms instant.
        var passes = RunRoom(manager, clock, clock.Ms + 10_000, period: 26);

        var fired = passes.Where(pass => Touches(pass, target)).Select(pass => pass.Slot).ToArray();
        Assert.True(fired.Length >= 49, $"{fired.Length} firings");
        Assert.All(fired.Zip(fired.Skip(1), (previous, next) => next - previous), gap => Assert.Equal(200, gap));
    }

    // The game loop wakes at each 25 ms instant, and a fraction of a ms after each room tick, a little less late each time.
    private static Func<long, double> LessLateEachRoomTick(long start) => ms => ms % 500 == 0 ? 0.9 - 0.1 * ((ms - start) / 500 % 9) : 0;

    [Fact]
    public void EachRoomTickStartsOnThePollAtItsBoundaryWhenThatPollIsLessLateThanTheLastOne()
    {
        var (clock, manager) = GridRoom();
        ExecutorActor(1, 0);

        var passes = RunRoom(manager, clock, clock.Ms + 5000, period: 25, LessLateEachRoomTick(clock.Ms));

        Assert.Equal(10, passes.Count(pass => pass.Full));
        Assert.All(passes, pass => Assert.True(pass.Full == (pass.At % 500 == 0), $"pass at {pass.At % 500} ms after the room tick, full {pass.Full}"));
    }

    [Fact]
    public void AWalkOnDelayOfOneGoesOutInItsRoomTickAfterTheUserUpdateWhenThePollIsLessLateThanTheLastOne()
    {
        var (clock, manager) = GridRoom();
        var target = GridWalkOnStack(delay: 1);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25, LessLateEachRoomTick(clock.Ms));

        var step = Assert.Single(passes, pass => pass.Full && HasUserUpdate(pass) && StatusOf(pass, actor).Contains("mv 1,1,"));
        var output = Assert.Single(passes, pass => Touches(pass, target));
        Assert.True(output.Full, $"output in a fast pass {output.At - step.At} ms after the step");
        Assert.Equal(step.At + 500, output.At);
        AssertUserUpdateFirst(output, target);
    }

    [Fact]
    public void AWalkOnThatADelayedMoveUserRaisesInTheRoomTickWaitsOneFastPassWhileTheStepWalkGoesOutInItsTick()
    {
        var (clock, manager) = GridRoom();
        var stepTarget = GridWalkOnStack(delay: 0);
        var moveTarget = ToggleAt(GridMoveTarget, 3, 3);
        ExecutorFloor(GridMoveTile, 0, 3);
        var move = WiredBox(GridMoveBox, "wf_act_user_to_furni", 3, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(move,
            new() { IntParams = [100, 0, 1], SelectedItems = [GridMoveTile], Delay = 1 }, out var config, out var error), error);
        move.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(move));
        GridStack(GridMoveTileStack, "wf_trg_walks_on_furni", new() { IntParams = [100], SelectedItems = [GridMoveTile] }, 2,
            moveTarget, 0);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 1);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);

        var step = Assert.Single(passes, pass => Touches(pass, stepTarget));
        Assert.True(step.Full);
        Assert.Contains("mv 1,1,", StatusOf(step, actor));
        AssertUserUpdateFirst(step, stepTarget);
        var output = Assert.Single(passes, pass => Touches(pass, moveTarget));
        Assert.False(output.Full, $"output in the room tick {output.At - step.At} ms after the step");
        Assert.Equal(step.At + 550, output.At);
    }

    [Fact]
    public void ADelayThatEndsOnARoomTickArmsNoWakeUp()
    {
        var (clock, manager) = GridRoom();
        var wakes = RecordWakes(clock);
        var target = GridWalkOnStack(delay: 1);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 3);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);

        Assert.Single(passes, pass => Touches(pass, target));
        Assert.Empty(wakes);
    }

    [Fact]
    public void ADelayThatEndsOffTheRoomTicksArmsOneWakeUpBeforeItsDueTime()
    {
        var (clock, manager) = GridRoom();
        var wakes = RecordWakes(clock);
        var stepTarget = GridWalkOnStack(delay: 0);
        GridMoveUserStack(heldWalkDelay: 1);
        var actor = ExecutorActor(1, 0);
        actor.MoveTo(1, 1);

        var passes = RunRoom(manager, clock, clock.Ms + 2500, period: 25);

        // The move lands in the room tick 500 ms after the step; the walk it raises fires 50 ms later and its delay ends 500 ms after that.
        var step = Assert.Single(passes, pass => Touches(pass, stepTarget));
        var wake = Assert.Single(wakes);
        Assert.InRange(wake.At + wake.AfterMs, step.At + 1001, step.At + 1050);
    }

    private ConcurrentQueue<(long At, double AfterMs)> RecordWakes(GridClock clock)
    {
        var wakes = new ConcurrentQueue<(long At, double AfterMs)>();
        _room.GetWired().WakeAfter = (afterMs, _) => wakes.Enqueue((clock.Ms, afterMs));

        return wakes;
    }

    // A delay of one on the Walks On stack of (1, 1) moves the user onto a tile at (0, 3), whose own Walks On stack toggles a furni.
    private void GridMoveUserStack(int heldWalkDelay)
    {
        var target = ToggleAt(GridMoveTarget, 3, 3);
        ExecutorFloor(GridMoveTile, 0, 3);
        var move = WiredBox(GridMoveBox, "wf_act_user_to_furni", 3, 0);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(move,
            new() { IntParams = [100, 0, 1], SelectedItems = [GridMoveTile], Delay = 1 }, out var config, out var error), error);
        move.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(move));
        GridStack(GridMoveTileStack, "wf_trg_walks_on_furni", new() { IntParams = [100], SelectedItems = [GridMoveTile] }, 2, target,
            heldWalkDelay);
    }

    [Theory]
    [InlineData(1000.0, 25)]
    [InlineData(1000.2, 25)]
    [InlineData(1012.3, 13)]
    [InlineData(1024.9, 1)]
    public void TheGameLoopWakesOnTheTwentyFiveMsGridOfTheMonotonicClock(double nowMs, int sleepMs)
    {
        Assert.Equal(sleepMs, Game.PollDelay(nowMs));
    }

    private (GridClock Clock, RoomManager Manager) GridRoom()
    {
        var clock = new GridClock();
        var wired = new WiredComponent(_room, TestLogging.Logger, clock, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance,
            new EmptyConfigurationStore(), _database, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty,
            TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        Set("_wiredComponent", wired);
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_now", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Func<long>(() => clock.Ms));

        var manager = (RoomManager)RuntimeHelpers.GetUninitializedObject(typeof(RoomManager));
        void SetManager(string field, object value) =>
            typeof(RoomManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);
        SetManager("_clock", clock);
        SetManager("_rooms", new ConcurrentDictionary<uint, Room>([new(_room.Id, _room)]));
        SetManager("_fastWiredRooms", new ConcurrentDictionary<uint, Room>([new(_room.Id, _room)]));
        SetManager("_pendingFullRooms", new ConcurrentDictionary<uint, Room>());

        return (clock, manager);
    }

    // A walk tile at (1, 1) whose Walks On stack toggles the target.
    private Item GridWalkOnStack(int delay)
    {
        ExecutorFloor(GridWalkTile, 1, 1);
        var target = ToggleAt(GridTarget, 2, 0);
        GridStack(GridWalkStack, "wf_trg_walks_on_furni", new() { IntParams = [100], SelectedItems = [GridWalkTile] }, 0, target, delay);

        return target;
    }

    private Item GridTimerStack(string name, int units)
    {
        var target = ToggleAt(GridTarget, 2, 0);
        GridStack(GridTimerStackId, name, new() { IntParams = [units] }, 1, target, 0);

        return target;
    }

    private void GridStack(uint id, string name, Plus.HabboHotel.Items.Wired.Configuration.WiredConfiguration proposed, int y, Item target, int delay)
    {
        var trigger = WiredBox(id, name, 3, y);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger, proposed, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(trigger));
        var toggle = WiredBox(id + 1, "wf_act_toggle_state", 3, y);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(toggle, new() { IntParams = [0, 100], SelectedItems = [target.Id], Delay = delay },
            out config, out error), error);
        toggle.ApplyConfiguration(config);
        Assert.True(_room.GetWired().AddBox(toggle));
    }

    // Polls the room manager as the game loop does and returns each pass it started, with its start time and what the client got.
    private List<GridPass> RunRoom(RoomManager manager, GridClock clock, long until, int period, Func<long, double>? lateness = null)
    {
        var passes = new List<GridPass>();
        var roomTick = typeof(RoomManager).GetField("_cycleLastExecution", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _client.Packets.Clear();

        for (; clock.Ms < until; clock.Ms += period) {
            clock.SubMs = lateness?.Invoke(clock.Ms) ?? 0;
            var previous = _room.ProcessTask;
            var tick = roomTick.GetValue(manager);
            var seen = _client.Packets.Count;
            manager.OnCycle();

            if (ReferenceEquals(previous, _room.ProcessTask)) {
                continue;
            }

            Assert.True(_room.ProcessTask!.Wait(TimeSpan.FromSeconds(5)));
            passes.Add(new(clock.Ms, !Equals(tick, roomTick.GetValue(manager)), [.. _client.Packets.Skip(seen)]));
        }

        return passes;
    }

    private static bool HasUserUpdate(GridPass pass) => pass.Packets.Any(packet => packet.Header == ServerPacketHeader.UserUpdateComposer);

    private static bool Touches(GridPass pass, Item item) => pass.Packets.Any(packet => DataUpdateIds(packet).Contains(item.Id));

    private static void AssertUserUpdateFirst(GridPass pass, Item item)
    {
        var headers = pass.Packets.Select(packet => packet.Header).ToList();
        var update = headers.IndexOf(ServerPacketHeader.UserUpdateComposer);
        var output = pass.Packets.ToList().FindIndex(packet => DataUpdateIds(packet).Contains(item.Id));
        Assert.True(update >= 0 && update < output, $"pass at {pass.At}: " + string.Join(",", headers));
    }

    private static uint[] DataUpdateIds((uint Header, byte[] Body) packet)
    {
        if (packet.Header != ServerPacketHeader.ObjectsDataUpdateComposer) {
            return [];
        }

        var input = new FlashIncomingPacket { Buffer = packet.Body };

        return [.. Enumerable.Range(0, input.ReadInt()).Select(_ =>
        {
            var id = (uint)input.ReadInt();
            input.ReadInt();
            input.ReadString();

            return id;
        })];
    }

    private static string StatusOf(GridPass pass, RoomUser actor)
    {
        foreach (var packet in pass.Packets.Where(packet => packet.Header == ServerPacketHeader.UserUpdateComposer)) {
            var body = new FlashIncomingPacket { Buffer = packet.Body };

            for (var count = body.ReadInt(); count > 0; count--) {
                var id = body.ReadInt();
                body.ReadInt();
                body.ReadInt();
                body.ReadString();
                body.ReadInt();
                body.ReadInt();
                body.ReadInt();
                var status = body.ReadString();

                if (id == actor.VirtualId) {
                    return status;
                }
            }
        }

        return "";
    }

    private sealed record GridPass(long At, bool Full, (uint Header, byte[] Body)[] Packets)
    {
        public long Slot => At - At % 50;
    }

    // Wall time and the monotonic timestamp move together from a 500 ms boundary. The Wired engine reads whole milliseconds; the monotonic
    // timestamp also has the fraction of a millisecond the game loop wakes late by.
    private sealed class GridClock : TimeProvider
    {
        public long Ms { get; set; } = 1_000_000;
        public double SubMs { get; set; }
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddMilliseconds(Ms + SubMs);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        public override long TimestampFrequency => 1_000_000;
        public override long GetTimestamp() => Ms * 1000 + (long)(SubMs * 1000);
    }
}
