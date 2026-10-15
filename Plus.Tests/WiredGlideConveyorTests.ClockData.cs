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
        Assert.Equal(1456u, ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.Equal(80u, ServerPacketHeader.ObjectsDataUpdateComposer);
    }

    [Fact]
    public void ObjectDataUpdateHeadersAreMappedByTheCurrentRevisionAndItsExampleTable()
    {
        var revision = new RevisionsCache().InternalRevision;
        using var example = JsonDocument.Parse(File.ReadAllText(HabbiconPacketTests.Repo("Resources/Revisions/example.json")));
        var outgoing = example.RootElement.GetProperty("OutgoingHeaders");

        Assert.Equal("WIN63-202610121709-785561319", revision.Name);
        Assert.Equal(1456u, revision.InternalIdToOutgoingIdMapping[ServerPacketHeader.ObjectDataUpdateComposer]);
        Assert.Equal(80u, revision.InternalIdToOutgoingIdMapping[ServerPacketHeader.ObjectsDataUpdateComposer]);
        Assert.Equal(1456u, outgoing.GetProperty("ObjectDataUpdateComposer").GetUInt32());
        Assert.Equal(80u, outgoing.GetProperty("ObjectsDataUpdateComposer").GetUInt32());
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
        Assert.Equal([((uint)CounterId, "0")], ReadBatch(ordered[0]));
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
        Assert.Equal([((uint)CounterId, "0")], ReadBatch(batch));
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.Empty(ObjectUpdates(frames));
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
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            if (frames[^1].Header == ServerPacketHeader.ObjectsDataUpdateComposer) {
                generations.Add(item.StateGeneration);
            }

            return false;
        };

        f.Advance(1000);

        var batches = frames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(ReadBatch).ToArray();
        Assert.True(batches.Length >= 3);
        Assert.All(batches, batch => Assert.Equal([((uint)CounterId, "1")], batch));
        // Only the first Set changed the display; the repeated unchanged Sets did no state, DB or navigation work.
        Assert.True(generations.Count >= 3);
        Assert.Single(generations.Distinct());
        Assert.Empty(frames.Where(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer));
        Assert.Empty(ObjectUpdates(frames));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void WiredIncreaseAndDecreaseKeepTheObjectUpdate(int operation)
    {
        var f = ClockFixture("wf_upcounter1", "wf_act_adjust_clock", [operation, 100, 0, 2]);
        var rider = f.Walker(1, 4, 10, 4, 11);
        PresetCounter(f, 10);
        var frames = CaptureFrames(rider);

        f.Advance(250);

        Assert.NotEmpty(ObjectUpdates(frames));
        Assert.Empty(frames.Where(frame => frame.Header is ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer));
    }

    [Theory]
    [InlineData("wf_game_upcounter1")]
    [InlineData("bb_counter")]
    public void WiredControlOfGameCountersAndTimersKeepsTheObjectUpdate(string counter)
    {
        var f = ClockFixture(counter, "wf_act_control_clock", [0, 100]);
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

        Assert.Equal(["5", "10"], mappedFrames.Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(frame => ReadBatch(frame).Single().Text));
        Assert.Empty(ObjectUpdates(mappedFrames));
        Assert.Equal(["5", "10"], ObjectUpdates(legacyFrames).Select(ReadObjectUpdateData));
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
            (game, WiredClockOrigin.ModernWired, WiredCounterPublication.Object),
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
            Single(clocks, c => c.Adjust(item, 2, 0, 2)), Single(clocks, c => c.SetPulseCount(item, 4)),
            Single(clocks, c => c.Adjust(item, 0, 0, 2, WiredClockOrigin.ModernWired))
        }) {
            Assert.Equal((WiredCounterPublication.Object, false), (change.Publication, change.ForceDisplay));
        }

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
            frames.Add((BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };

        return frames;
    }

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
