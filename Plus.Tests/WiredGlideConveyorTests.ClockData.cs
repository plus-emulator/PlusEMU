using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Revisions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const int CounterId = 20;

    [Fact]
    public void ObjectDataUpdateComposersWriteTheClientGrammarWithTheSept16Headers()
    {
        var single = TypedFurnitureStateTests.Payload(new Plus.Communication.Packets.Outgoing.Rooms.Engine.ObjectDataUpdateComposer(20, "7"));
        var batch = TypedFurnitureStateTests.Payload(new Plus.Communication.Packets.Outgoing.Rooms.Engine.ObjectsDataUpdateComposer([(20, "7"), (301, "12")]));

        // string decimal id, i32 stuff word0 (legacy format, no flags), legacy string.
        Assert.Equal([0, 2, (byte)'2', (byte)'0', 0, 0, 0, 0, 0, 1, (byte)'7'], single);
        // i32 count, then per object i32 id, i32 stuff word0, legacy string.
        Assert.Equal([0, 0, 0, 2,
            0, 0, 0, 20, 0, 0, 0, 0, 0, 1, (byte)'7',
            0, 0, 1, 45, 0, 0, 0, 0, 0, 2, (byte)'1', (byte)'2'], batch);
        Assert.Equal(3684u, ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.Equal(1330u, ServerPacketHeader.ObjectsDataUpdateComposer);
    }

    [Fact]
    public void ObjectDataUpdateHeadersAreMappedByTheCurrentRevisionAndItsExampleTable()
    {
        var revision = new RevisionsCache().InternalRevision;
        using var example = JsonDocument.Parse(File.ReadAllText(HabbiconPacketTests.Repo("Resources/Revisions/example.json")));
        var outgoing = example.RootElement.GetProperty("OutgoingHeaders");

        Assert.Equal("WIN63-202610151745-318700621", revision.Name);
        Assert.Equal(3684u, revision.InternalIdToOutgoingIdMapping[ServerPacketHeader.ObjectDataUpdateComposer]);
        Assert.Equal(1330u, revision.InternalIdToOutgoingIdMapping[ServerPacketHeader.ObjectsDataUpdateComposer]);
        Assert.Equal(3684u, outgoing.GetProperty("ObjectDataUpdateComposer").GetUInt32());
        Assert.Equal(1330u, outgoing.GetProperty("ObjectsDataUpdateComposer").GetUInt32());
        Assert.NotEqual(714u, ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.NotEqual(1211u, ServerPacketHeader.ObjectsDataUpdateComposer);
    }

    [Fact]
    public void WiredStartedPlainCounterTicksPublishSingleDataAndNoObjectUpdate()
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [0, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(rider);

        f.Advance(2600);

        var data = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer).Select(ReadSingle).ToArray();
        Assert.Equal(["0", "1", "2"], data.Select(entry => entry.Text).Take(3));
        Assert.All(data, entry => Assert.Equal((uint)CounterId, entry.Id));
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer));
        Assert.Empty(ObjectUpdates(frames));
    }

    [Fact]
    public void WiredStartOfAStoppedPlainCounterPublishesOneZeroBatchThenSingleTicks()
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [0, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        PresetCounter(f, 10);
        var frames = CaptureFrames(rider);

        f.Advance(2600);

        var ordered = frames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer).ToArray();
        Assert.Equal(ServerPacketHeader.ObjectsDataUpdateComposer, ordered[0].Header);
        Assert.Equal([((uint)CounterId, "0")], CounterEntries(ordered[0]));
        Assert.All(ordered.Skip(1), frame => Assert.Equal(ServerPacketHeader.ObjectDataUpdateComposer, frame.Header));
        Assert.Equal(["1", "2"], ordered.Skip(1).Select(frame => ReadSingle(frame).Text).Take(2));
        Assert.Empty(ObjectUpdates(frames));
    }

    [Fact]
    public void WiredResetPublishesABatchOnlyWhenTheDisplayChanges()
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [2, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        PresetCounter(f, 10);
        var frames = CaptureFrames(rider);

        f.Advance(1000);

        // The first firing resets 5 -> 0; the later firings find the display already 0 and publish nothing.
        var batch = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
        Assert.Equal([((uint)CounterId, "0")], CounterEntries(batch));
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.Empty(ObjectUpdates(frames));
    }

    // Observed on AIR WIN63-202610151745-318700621: a counter value written by Wired (Start reset, Reset, Adjust Clock) goes out in the
    // same ObjectsDataUpdate as the flashes of the event that wrote it, with each item once.
    [Theory]
    [InlineData(0, 10)]
    [InlineData(2, 10)]
    public void WiredCounterWritesShareTheBatchOfTheEventsFlashes(int operation, int preset)
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [operation, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        PresetCounter(f, preset);
        var batches = new List<(uint Id, string Text)[]>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.Add(ReadBatch(frame));
            }

            return false;
        };

        f.Advance(1000);

        var withCounter = batches.Where(batch => batch.Any(entry => entry.Id == CounterId)).ToArray();
        var first = withCounter[0];
        Assert.Equal([((uint)CounterId, "0")], first.Where(entry => entry.Id == CounterId));
        Assert.Contains(first, entry => entry.Id == 21u);
        Assert.Contains(first, entry => entry.Id == 22u);
        Assert.Equal(first.Length, first.Select(entry => entry.Id).Distinct().Count());
    }

    // A counter value that joins the event's data batch still raises its Counter event, so a counter-time trigger reacts to it as before.
    [Fact]
    public void WiredAdjustClockSetKeepsFeedingCounterTimeTriggersWhenTheWriteJoinsTheBatch()
    {
        string Config(int[] parameters, params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, "wf_upcounter1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_period_short", Config([4])),
            (22, 4, 12, 0.65, 0, "wf_act_adjust_clock", Config([2, 100, 0, 10], CounterId)),
            (23, 6, 12, 0.0, 0, "wf_trg_clock_counter", Config([0, 5, 100], CounterId)),
            (24, 6, 12, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [ObservedSubjectId], 100)),
            (ObservedSubjectId, 9, 9, 0.0, 0, "wf_colortile", null)
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        f.Walker(1, 4, 10, 4, 11);

        f.Advance(1000);

        Assert.Equal("1", f.Items[ObservedSubjectId].LegacyDataString);
    }

    [Fact]
    public void WiredSetPublishesABatchEvenWhenTheDisplayIsUnchangedWithoutStateWork()
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_adjust_clock", [2, 100, 0, 2]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var item = f.Items[CounterId];
        var frames = new List<(uint Header, byte[] Payload)>();
        var generations = new List<long>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (IsBoxFlashBatch(frame)) {
                return false;
            }

            frames.Add(frame);

            if (frames[^1].Header == ServerPacketHeader.ObjectsDataUpdateComposer) {
                generations.Add(item.StateGeneration);
            }

            return false;
        };

        f.Advance(1000);

        var batches = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(CounterEntries).ToArray();
        Assert.True(batches.Length >= 3);
        Assert.All(batches, batch => Assert.Equal([((uint)CounterId, "1")], batch));
        // Only the first Set changed the display; the repeated unchanged Sets did no state, DB or navigation work.
        Assert.True(generations.Count >= 3);
        Assert.Single(generations.Distinct());
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.Empty(ObjectUpdates(frames));
    }

    // Observed on AIR WIN63-202610151745-318700621: a Wired increase or decrease sends the new value in an
    // ObjectsDataUpdate, like a Wired set, and never a full object update.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WiredIncreaseAndDecreasePublishADataBatch(int operation)
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_adjust_clock", [operation, 100, 0, 2]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        PresetCounter(f, 10);
        var frames = CaptureFrames(rider);

        f.Advance(250);

        var batches = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(CounterEntries).ToArray();
        Assert.NotEmpty(batches);
        Assert.Equal(operation == 0 ? "6" : "4", batches[0].Single(entry => entry.Id == CounterId).Text);
        Assert.Empty(ObjectUpdates(frames));
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
    }

    [Fact]
    public void WiredStartedGameCounterTicksPublishSingleDataAndNoObjectUpdate()
    {
        // Observed on AIR WIN63-202610131318-860371703: wf_game_upcounter1 started by control_clock sends "1", "2", ... as ObjectDataUpdate.
        var f = ClockFixture("wf_game_upcounter1", "wf_act_control_clock", [0, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(rider);

        f.Advance(250);
        Assert.True(f.Engine.Remove(21));
        f.Advance(2350);

        var data = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer).Select(ReadSingle).ToArray();
        // The fixture counter starts with empty data, so its first half-second step publishes "0".
        Assert.Equal(["0", "1", "2"], data.Select(entry => entry.Text).Take(3));
        Assert.All(data, entry => Assert.Equal((uint)CounterId, entry.Id));
        Assert.Empty(ObjectUpdates(frames));
    }

    [Fact]
    public void WiredControlOfGameTimersKeepsTheObjectUpdate()
    {
        var f = ClockFixture("bb_counter", "wf_act_control_clock", [0, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var frames = CaptureFrames(rider);

        f.Advance(2600);

        Assert.NotEmpty(ObjectUpdates(frames));
        Assert.Empty(frames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer));
    }

    [Fact]
    public void GuiStartedPlainCounterTicksKeepTheObjectUpdate()
    {
        // The stack only resumes, which leaves a running counter (and its GUI provenance) alone.
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [4, 100]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.MovementContext().Room.GetWired().TryUseCounter(f.Items[CounterId], 0);
        var frames = CaptureFrames(rider);

        f.Advance(1600);

        Assert.NotEmpty(ObjectUpdates(frames));
        Assert.Empty(frames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer));
    }

    [Fact]
    public void ClientsWithoutTheDataHeadersGetTheObjectUpdateAndOthersTheDataFrame()
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_control_clock", [0, 100]);
        var mapped = f.Walker(1, 4, 10, 4, 11);
        var legacy = f.Walker(2, 5, 10, 5, 11);
        legacy.GetClient()!.Revision = new Revision
        {
            InternalIdToOutgoingIdMapping = typeof(ServerPacketHeader).GetFields()
                .Where(field => field.IsLiteral && field.FieldType == typeof(uint)).Select(field => (uint)field.GetRawConstantValue()!)
                .Where(id => id is not (ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer))
                .Distinct().ToDictionary(id => id)
        };
        var mappedFrames = CaptureFrames(mapped);
        var legacyFrames = CaptureFrames(legacy);

        f.Advance(2600);

        Assert.NotEmpty(mappedFrames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.Empty(ObjectUpdates(mappedFrames));
        Assert.NotEmpty(ObjectUpdates(legacyFrames));
        Assert.Empty(legacyFrames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer));
    }

    [Fact]
    public void QueuedSetsReachDataAndFallbackClientsWithTheirOwnTextWhenDrainedLater()
    {
        // The periodic action only adds zero, so it publishes nothing; the two Sets below are both queued before the first drain.
        var f = ClockFixture("wf_upcounter1", "wf_act_adjust_clock", [0, 100, 0, 0]);
        var mapped = f.Walker(1, 4, 10, 4, 11);
        var legacy = f.Walker(2, 5, 10, 5, 11);
        legacy.GetClient()!.Revision = new Revision
        {
            InternalIdToOutgoingIdMapping = typeof(ServerPacketHeader).GetFields()
                .Where(field => field.IsLiteral && field.FieldType == typeof(uint)).Select(field => (uint)field.GetRawConstantValue()!)
                .Where(id => id is not (ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer))
                .Distinct().ToDictionary(id => id)
        };
        var mappedFrames = CaptureFrames(mapped);
        var legacyFrames = CaptureFrames(legacy);
        var item = f.Items[CounterId];
        Assert.True(Counters(f).Adjust(item, 2, 0, 10, WiredClockOrigin.ModernWired));
        Assert.True(Counters(f).Adjust(item, 2, 0, 20, WiredClockOrigin.ModernWired));

        f.Advance(500);

        // Observed on AIR WIN63-202610151745-318700621: two writes of one counter in one event go out once, with the last value.
        Assert.Equal(["10"], mappedFrames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(frame => CounterEntries(frame).Single().Text));
        Assert.Empty(ObjectUpdates(mappedFrames));
        Assert.Equal(["10"], ObjectUpdates(legacyFrames).Select(ReadObjectUpdateData));
        Assert.Empty(legacyFrames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer));
        Assert.Equal("10", item.LegacyDataString);
    }

    [Fact]
    public void ControllerScopesDataPublicationByOriginAndClockKind()
    {
        var plain = ClockItem("wf_upcounter1", 101);
        var game = ClockItem("wf_game_upcounter1", 102);
        var timer = ClockItem("bb_counter", 103);
        var clocks = new WiredCounterController();

        foreach (var item in new[] { plain, game, timer }) {
            clocks.Attach(item);
        }

        foreach (var (item, origin, expected) in new[]
        {
            (plain, WiredClockOrigin.ModernWired, WiredCounterPublication.SingleData),
            (game, WiredClockOrigin.ModernWired, WiredCounterPublication.SingleData),
            (timer, WiredClockOrigin.ModernWired, WiredCounterPublication.Object)
        }) {
            Assert.True(clocks.Control(item, 0, 0, origin));
            clocks.TakeChanges();
            var tick = clocks.Poll(item == timer ? 1000 : 500).Single(change => ReferenceEquals(change.Item, item));
            Assert.Equal(expected, tick.Publication);
            Assert.False(tick.ForceDisplay);
        }

        var gui = ClockItem("wf_upcounter2", 104);
        clocks.Attach(gui);
        Assert.True(clocks.Control(gui, 0, 0, WiredClockOrigin.Gui));
        Assert.Equal(WiredCounterPublication.Object, clocks.Poll(500).Single(change => ReferenceEquals(change.Item, gui)).Publication);
    }

    [Fact]
    public void WiredStartKeepsAValueAdjustedAfterTheLastStopAndRestartsFromZeroAfterAStop()
    {
        // Observed on AIR WIN63-202610131318-860371703: set 30, start keeps 30 then counts 31, 32 ...; a start after a stop restarts from 0.
        var item = ClockItem("wf_upcounter1", 107);
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        Assert.True(clocks.Adjust(item, 2, 0, 60, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));
        Assert.Equal(30000, clocks.ReadMilliseconds(item));
        clocks.Poll(500);
        clocks.Poll(1000);
        Assert.Equal("31", item.LegacyDataString);
        Assert.True(clocks.Control(item, 1, 1000, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(item, 0, 1000, WiredClockOrigin.ModernWired));
        Assert.Equal(0, clocks.ReadMilliseconds(item));
    }

    [Fact]
    public void ControllerStartResetAndSetCarryTheirOwnDisplayTextAndForce()
    {
        var item = ClockItem("wf_upcounter1", 105);
        var clocks = new WiredCounterController();
        clocks.Attach(item);
        Assert.True(clocks.Adjust(item, 2, 0, 10, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Adjust(item, 2, 0, 20, WiredClockOrigin.ModernWired));
        var sets = clocks.TakeChanges();
        // Both Sets were queued before the drain: each carries its own text, not the item's final string.
        Assert.Equal(["5", "10"], sets.Select(change => change.Text));
        Assert.All(sets, change => Assert.Equal((WiredCounterPublication.BatchData, true), (change.Publication, change.ForceDisplay)));
        Assert.Equal("10", item.LegacyDataString);

        Assert.True(clocks.Adjust(item, 2, 0, 20, WiredClockOrigin.ModernWired));
        var unchanged = Assert.Single(clocks.TakeChanges());
        Assert.False(unchanged.DisplayChanged);
        Assert.True(unchanged.ForceDisplay);

        foreach (var change in new[]
        {
            Single(clocks, c => c.Adjust(item, 0, 0, 2)), Single(clocks, c => c.Adjust(item, 1, 0, 2)),
            Single(clocks, c => c.Adjust(item, 2, 0, 2)), Single(clocks, c => c.SetPulseCount(item, 4))
        }) {
            Assert.Equal((WiredCounterPublication.Object, false), (change.Publication, change.ForceDisplay));
        }

        foreach (var change in new[]
        {
            Single(clocks, c => c.Adjust(item, 0, 0, 2, WiredClockOrigin.ModernWired)),
            Single(clocks, c => c.Adjust(item, 1, 0, 4, WiredClockOrigin.ModernWired))
        }) {
            Assert.Equal((WiredCounterPublication.BatchData, false), (change.Publication, change.ForceDisplay));
        }

        // A Start right after an adjust keeps the value; the Start after a Stop restarts from zero.
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));
        Assert.Empty(clocks.TakeChanges());
        Assert.True(clocks.Control(item, 1, 0, WiredClockOrigin.ModernWired));
        Assert.True(clocks.Control(item, 0, 0, WiredClockOrigin.ModernWired));
        var start = Assert.Single(clocks.TakeChanges());
        Assert.Equal((WiredCounterPublication.BatchData, false, "0"), (start.Publication, start.ForceDisplay, start.Text));
        Assert.True(start.DisplayChanged);
        Assert.Equal(WiredCounterPublication.SingleData, Assert.Single(clocks.Poll(500)).Publication);
        Assert.True(clocks.Control(item, 0, 600, WiredClockOrigin.ModernWired));
        Assert.Empty(clocks.TakeChanges());

        var zero = ClockItem("wf_upcounter1", 106);
        clocks.Attach(zero);
        Assert.True(clocks.Control(zero, 2, 0, WiredClockOrigin.ModernWired));
        var reset = Assert.Single(clocks.TakeChanges(), change => ReferenceEquals(change.Item, zero));
        Assert.Equal((WiredCounterPublication.BatchData, false, false), (reset.Publication, reset.ForceDisplay, reset.DisplayChanged));
    }

    private static WiredCounterChange Single(WiredCounterController clocks, Func<WiredCounterController, bool> operation)
    {
        Assert.True(operation(clocks));

        return Assert.Single(clocks.TakeChanges());
    }

    private static Item ClockItem(string name, uint id) => new()
    {
        Id = id,
        ExtraData = new LegacyDataFormat { Data = "0" },
        Definition = new()
        {
            Type = ItemType.Floor,
            ItemName = name,
            InteractionName = name == "bb_counter" ? "bb_counter" : name,
            InteractionType = name == "bb_counter" ? InteractionType.Banzaicounter : InteractionType.None,
            Width = 1,
            Length = 1,
            Modes = 2,
            AdjustableHeights = [],
            VendingIds = [],
            PublicName = name
        }
    };

    // Observed on AIR WIN63-202610131318-860371703: a started counter shows its first "1" on the 2nd 500 ms room boundary strictly after
    // the firing (501-1000 ms later; exactly 1000 ms for an on-boundary firing), then ticks every 1000 ms on that grid.
    [Theory]
    [InlineData(150, 1000)]
    [InlineData(450, 1000)]
    [InlineData(500, 1500)]
    [InlineData(550, 1500)]
    public void WiredStartedCounterShowsItsFirstSecondOnTheSecondRoomBoundaryAfterTheFiring(int fireAt, int firstSecondAt)
    {
        string Config(int[] parameters, string text = "", params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            Text = text,
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, "wf_upcounter1", null),
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", Config([1, 0, 0], "cstart")),
            (22, 4, 12, 0.65, 0, "wf_act_control_clock", Config([0, 100], "", CounterId))
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var counter = f.Items[CounterId];

        f.Advance(fireAt);
        f.Owned(() => rider.OnChat(0, "cstart", false));
        f.Advance(firstSecondAt - fireAt - 50);
        Assert.Equal("0", counter.LegacyDataString is "" ? "0" : counter.LegacyDataString);
        f.Advance(50);
        Assert.Equal("1", counter.LegacyDataString);
        f.Advance(950);
        Assert.Equal("1", counter.LegacyDataString);
        f.Advance(50);
        Assert.Equal("2", counter.LegacyDataString);
    }

    private static Fixture ClockFixture(string counter, string action, int[] actionParams)
    {
        string Config(int[] parameters, params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (CounterId, 8, 12, 0.0, 0, counter, null),
            (21, 4, 12, 0.0, 0, "wf_trg_period_short", Config([4])),
            (22, 4, 12, 0.65, 0, action, Config(actionParams, CounterId))
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);

        return f;
    }

    private static WiredCounterController Counters(Fixture f) => (WiredCounterController)typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent)
        .GetField("_counters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.MovementContext().Room.GetWired())!;

    // Sets a non-zero display through the GUI-origin path so the preset publishes before the capture starts.
    private static void PresetCounter(Fixture f, int halfSeconds)
    {
        Assert.True(Counters(f).SetPulseCount(f.Items[CounterId], halfSeconds));
        Counters(f).TakeChanges();
        f.Items[CounterId].LegacyDataString = (halfSeconds / 2).ToString();
    }

    private static List<(uint Header, byte[] Payload)> CaptureFrames(RoomUser user)
    {
        var frames = new List<(uint, byte[])>();
        user.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (!IsBoxFlashBatch(frame)) {
                frames.Add(frame);
            }

            return false;
        };

        return frames;
    }

    // The boxes' own flash states go out in batches of their own; these tests follow the counter's data.
    private static bool IsBoxFlashBatch((uint Header, byte[] Payload) frame) =>
        frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer && ReadBatch(frame).All(entry => entry.Id != CounterId);

    private static (uint Id, string Text)[] CounterEntries((uint Header, byte[] Payload) frame) => [.. ReadBatch(frame).Where(entry => entry.Id == CounterId)];

    private static (uint Header, byte[] Payload)[] ObjectUpdates(List<(uint Header, byte[] Payload)> frames) => frames
        .Where(frame => frame.Header == ServerPacketHeader.ObjectUpdateComposer && BinaryPrimitives.ReadInt32BigEndian(frame.Payload) == CounterId).ToArray();

    // Floor object update: id, sprite, x, y, rotation, z, height, floor extra, then the data word and legacy string.
    private static string ReadObjectUpdateData((uint Header, byte[] Payload) frame)
    {
        var offset = 20;

        for (var i = 0; i < 2; i++) {
            offset += 2 + BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(offset));
        }

        offset += 4;
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(offset)));
        var length = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(offset + 4));

        return Encoding.UTF8.GetString(frame.Payload, offset + 6, length);
    }

    private static (uint Id, string Text) ReadSingle((uint Header, byte[] Payload) frame)
    {
        Assert.Equal(ServerPacketHeader.ObjectDataUpdateComposer, frame.Header);
        var idLength = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload);
        var id = uint.Parse(Encoding.UTF8.GetString(frame.Payload, 2, idLength));
        var offset = 2 + idLength;
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(offset)));
        var textLength = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(offset + 4));
        var text = Encoding.UTF8.GetString(frame.Payload, offset + 6, textLength);
        Assert.Equal(offset + 6 + textLength, frame.Payload.Length);

        return (id, text);
    }

    private static (uint Id, string Text)[] ReadBatch((uint Header, byte[] Payload) frame)
    {
        Assert.Equal(ServerPacketHeader.ObjectsDataUpdateComposer, frame.Header);
        var count = BinaryPrimitives.ReadInt32BigEndian(frame.Payload);
        var offset = 4;
        var entries = new List<(uint, string)>();

        for (var i = 0; i < count; i++) {
            var id = BinaryPrimitives.ReadUInt32BigEndian(frame.Payload.AsSpan(offset));
            Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(frame.Payload.AsSpan(offset + 4)));
            var length = BinaryPrimitives.ReadUInt16BigEndian(frame.Payload.AsSpan(offset + 8));
            entries.Add((id, Encoding.UTF8.GetString(frame.Payload, offset + 10, length)));
            offset += 10 + length;
        }

        Assert.Equal(offset, frame.Payload.Length);

        return [.. entries];
    }
}
