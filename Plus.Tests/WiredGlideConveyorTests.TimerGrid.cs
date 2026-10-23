using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint GridTileId = 40;

    // Under a 50 ms timer the toggled furni's state goes out in a data batch on every tick; no full object update is sent for it.
    [Fact]
    public void AFiftyMillisecondTimerPublishesTheToggledStateInADataBatchEveryTick()
    {
        var (f, rider) = TimerGridRoom("wf_trg_period_short");
        var batches = new List<(int At, string State)>();
        var objectUpdates = 0;
        var now = 0;
        rider.GetClient()!.SendCallback = args =>
        {
            var bytes = args.MemoryBuffer.ToArray();
            var frame = (BinaryPrimitives.ReadUInt32BigEndian(bytes), bytes[6..]);

            if (frame.Item1 == ServerPacketHeader.ObjectsDataUpdateComposer) {
                batches.AddRange(ReadBatch(frame).Where(entry => entry.Id == GridTileId).Select(entry => (now, entry.Text)));
            }
            else if (frame.Item1 == ServerPacketHeader.ObjectUpdateComposer && BinaryPrimitives.ReadInt32BigEndian(frame.Item2) == GridTileId) {
                objectUpdates++;
            }

            return false;
        };

        for (var i = 0; i < 20; i++) {
            f.Advance(50);
            now += 50;
        }

        Assert.Equal(0, objectUpdates);
        Assert.True(batches.Count >= 18, $"{batches.Count} tile states");
        Assert.Equal(batches.Count, batches.Select(entry => entry.At).Distinct().Count());
        Assert.Equal(f.Items[GridTileId].LegacyDataString, batches[^1].State);
    }

    // A 500 ms timer keeps the room's 500 ms grid whatever its arming time, so its toggles keep one fixed phase on that grid, 500
    // ms apart. The effect runs in the room tick the timer fires on.
    [Fact]
    public void AFiveHundredMillisecondTimerTogglesOnTheRoomTicks()
    {
        var (f, _) = TimerGridRoom("wf_trg_periodically");
        var tile = f.Items[GridTileId];
        var changes = new List<int>();
        var last = tile.LegacyDataString;

        for (var now = 50; now <= 3000; now += 50) {
            f.Advance(50);

            if (tile.LegacyDataString != last) {
                changes.Add(now);
                last = tile.LegacyDataString;
            }
        }

        Assert.Equal([500, 1000, 1500, 2000, 2500, 3000], changes);
    }

    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Rider) TimerGridRoom(string timer)
    {
        string Config(int[] parameters, params uint[] selected) => JsonSerializer.Serialize(new WiredConfiguration
        {
            IntParams = [.. parameters],
            SelectedItems = [.. selected]
        });
        var f = new Fixture(
        [
            (GridTileId, 8, 12, 0.0, 0, "wf_colortile", null),
            (41, 4, 12, 0.0, 0, timer, Config([1])),
            (42, 4, 12, 0.65, 0, "wf_act_toggle_state", Config([0, 100], GridTileId))
        ], live: true);

        return (f, f.Walker(1, 4, 10, 4, 11));
    }
}
