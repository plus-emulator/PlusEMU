using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint ResetBlob = 41, ResetBlob2 = 42, ResetBlob2AtZero = 43, ResetSay = 45, ResetToggle = 46;

    private static Fixture BlobResetFixture(params uint[] picks)
    {
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (CounterId, 8, 12, 0.0, 0, "wf_game_upcounter1", null),
            (ResetBlob, 6, 12, 0.0, 0, "wf_blob", null),
            (ResetBlob2, 5, 12, 0.0, 0, "wf_blob2", null),
            (ResetBlob2AtZero, 4, 12, 0.0, 0, "wf_blob2", null),
            (ResetSay, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (ResetToggle, 4, 9, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], picks, 100))
        };
        var f = new Fixture(layout, live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[CounterId]);
        f.Items[CounterId].LegacyDataString = "0";

        foreach (var id in new[] { ResetBlob, ResetBlob2, ResetBlob2AtZero }) {
            f.Items[id].Definition.Modes = 2;
            f.Items[id].ExtraData = new LegacyDataFormat { Data = id == ResetBlob2AtZero ? "0" : "1" };
        }

        return f;
    }

    private static (int At, uint Header, byte[] Payload)[] FireAndCapture(Fixture f, RoomUser rider, int milliseconds)
    {
        var frames = new List<(int At, uint Header, byte[] Payload)>();
        var elapsed = 0;
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((elapsed, BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };
        f.Advance(1150);
        frames.Clear();
        f.Owned(() => rider.OnChat(0, "tg", false));

        for (; elapsed < milliseconds; elapsed += 50) {
            f.Advance(50);
        }

        return [.. frames];
    }

    private static (int At, (uint Id, string Text)[] Entries)[] BlobBatches((int At, uint Header, byte[] Payload)[] frames) => frames
        .Where(frame => frame.Header == ServerPacketHeader.ObjectsDataUpdateComposer)
        .Select(frame => (frame.At, Entries: ReadBatch((frame.Header, frame.Payload)).Where(entry => entry.Id is ResetBlob or ResetBlob2 or ResetBlob2AtZero).ToArray()))
        .Where(batch => batch.Entries.Length > 0).ToArray();

    // A Wired start of a game counter sets every blob to state 0, in an ObjectsDataUpdate on the room tick before the counter's first value.
    [Fact]
    public void StartingAGameCounterWithToggleStateSetsEveryBlobToZeroOnTheNextRoomTick()
    {
        var f = BlobResetFixture(CounterId);
        var rider = f.Walker(1, 6, 10, 6, 11);

        var frames = FireAndCapture(f, rider, 3000);

        var batch = Assert.Single(BlobBatches(frames));
        Assert.Equal([ResetBlob, ResetBlob2, ResetBlob2AtZero], batch.Entries.Select(entry => entry.Id).Order());
        Assert.All(batch.Entries, entry => Assert.Equal("0", entry.Text));
        var first = frames.First(frame => frame.Header == ServerPacketHeader.ObjectDataUpdateComposer && ReadSingle((frame.Header, frame.Payload)).Id == CounterId);
        Assert.InRange(first.At - batch.At, 450, 550);
        Assert.Equal(["0", "0", "0"], new[] { ResetBlob, ResetBlob2, ResetBlob2AtZero }.Select(id => f.Items[id].LegacyDataString));
    }

    // A blob that the starting Toggle State selects keeps the behaviour without the reset: it toggles from its own state.
    [Fact]
    public void ABlobThatTheStartingToggleStateSelectsKeepsMasterBehaviour()
    {
        var f = BlobResetFixture(CounterId, ResetBlob);
        var rider = f.Walker(1, 6, 10, 6, 11);

        var frames = FireAndCapture(f, rider, 3000);

        var entries = Assert.Single(BlobBatches(frames)).Entries;
        Assert.Equal("0", Assert.Single(entries, entry => entry.Id == ResetBlob).Text);
        Assert.Equal("0", Assert.Single(entries, entry => entry.Id == ResetBlob2).Text);
        Assert.Equal("0", f.Items[ResetBlob].LegacyDataString);
    }

    // A later firing neither restarts the counter nor resets the blobs again.
    [Fact]
    public void ALaterToggleStateFiringLeavesTheBlobsAlone()
    {
        var f = BlobResetFixture(CounterId);
        var rider = f.Walker(1, 6, 10, 6, 11);
        FireAndCapture(f, rider, 3000);
        f.Items[ResetBlob].LegacyDataString = "1";

        var frames = new List<(int At, uint Header, byte[] Payload)>();
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            frames.Add((0, BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]));

            return false;
        };
        f.Owned(() => rider.OnChat(0, "tg", false));
        f.Advance(3000);

        Assert.Empty(BlobBatches([.. frames]));
        Assert.Equal("1", f.Items[ResetBlob].LegacyDataString);
    }

    // Toggle State that does not start a counter does not reset the blobs.
    [Fact]
    public void ToggleStateWithoutAGameCounterLeavesTheBlobsAlone()
    {
        var f = BlobResetFixture(ResetBlob2AtZero);
        var rider = f.Walker(1, 6, 10, 6, 11);

        var frames = FireAndCapture(f, rider, 3000);

        Assert.Equal([(ResetBlob2AtZero, "1")], Assert.Single(BlobBatches(frames)).Entries);
        Assert.Equal(["1", "1"], new[] { ResetBlob, ResetBlob2 }.Select(id => f.Items[id].LegacyDataString));
        Assert.False(Counters(f).IsRunning(f.Items[CounterId]));
    }
}
