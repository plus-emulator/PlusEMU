using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

/// <summary>The official monitor protocol: statistics and error rows as the AIR parsers read them, the caps and the decaying cost.</summary>
public sealed class WiredMonitorProtocolTests
{
    private static readonly string[] RowNames =
    [
        "EXECUTION_CAP", "DELAYED_EVENTS_CAP", "EXECUTOR_OVERLOAD", "MARKED_AS_HEAVY", "KILLED", "RECURSION_TIMEOUT",
        "TOO_MANY_VARIABLES", "TRANSACTION_FAILURE", "PLACEMENT_FAILURE", "VARIABLE_FX_LIMIT"
    ];

    [Fact]
    public void TheMonitorPacketsTakeTheirOfficialIdsInTheHeaderTablesAndTheRevisionExample()
    {
        Assert.Equal(2907u, ClientPacketHeader.GetWiredRoomStatsEvent);
        Assert.Equal(441u, ClientPacketHeader.GetWiredErrorLogsEvent);
        Assert.Equal(1714u, ClientPacketHeader.ClearWiredErrorLogsEvent);
        Assert.Equal(1014u, ServerPacketHeader.WiredRoomStatsComposer);
        Assert.Equal(3349u, ServerPacketHeader.WiredErrorLogsComposer);

        using var table = HabbiconPacketTests.CurrentRevisionTable();
        var incoming = table.RootElement.GetProperty("IncomingHeaders");
        var outgoing = table.RootElement.GetProperty("OutgoingHeaders");
        Assert.Equal(2907u, incoming.GetProperty(nameof(ClientPacketHeader.GetWiredRoomStatsEvent)).GetUInt32());
        Assert.Equal(441u, incoming.GetProperty(nameof(ClientPacketHeader.GetWiredErrorLogsEvent)).GetUInt32());
        Assert.Equal(1714u, incoming.GetProperty(nameof(ClientPacketHeader.ClearWiredErrorLogsEvent)).GetUInt32());
        Assert.Equal(1014u, outgoing.GetProperty(nameof(ServerPacketHeader.WiredRoomStatsComposer)).GetUInt32());
        Assert.Equal(3349u, outgoing.GetProperty(nameof(ServerPacketHeader.WiredErrorLogsComposer)).GetUInt32());
    }

    [Fact]
    public void RoomStatsAreTwoDoublesABooleanAndTenIntegersInTheParserOrder()
    {
        var stats = WiredRoomStats.Create(1.0075, 3125, 151, 0, 1, 2, 3);
        var body = Write(new WiredRoomStatsComposer(stats).Compose);

        Assert.Equal(8 + 8 + 1 + 10 * 4, body.Length);
        Assert.Equal(1.0075, BinaryPrimitives.ReadDoubleBigEndian(body.AsSpan(0)));
        Assert.Equal(3125d, BinaryPrimitives.ReadDoubleBigEndian(body.AsSpan(8)));
        Assert.Equal(0, body[16]);
        var ints = Enumerable.Range(0, 10).Select(i => BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(17 + i * 4))).ToArray();
        Assert.Equal([151, 4000, 0, 4000, 1, 15, 2, 62, 3, 46], ints);
    }

    [Theory]
    [InlineData(3125, 15, 62, 46)]
    [InlineData(4687.5, 23, 93, 70)]
    [InlineData(2500, 12, 50, 37)]
    [InlineData(6563, 32, 131, 98)]
    public void ThePermanentVariableCapsFollowTheCostCap(double cap, int furni, int user, int global)
    {
        var stats = WiredRoomStats.Create(0, cap, 0, 0, 0, 0, 0);

        Assert.Equal((4000, 4000), (stats.FloorItemCap, stats.WallItemCap));
        Assert.Equal((furni, user, global), (stats.MaxPermanentFurniVariables, stats.MaxPermanentUserVariables, stats.MaxPermanentGlobalVariables));
    }

    // The cap is 312.5 x (owner account level + 2). A level below 1 counts as 1.
    [Theory]
    [InlineData(6, 2500)]
    [InlineData(8, 3125)]
    [InlineData(9, 3437.5)]
    [InlineData(13, 4687.5)]
    [InlineData(19, 6562.5)]
    [InlineData(1, 937.5)]
    [InlineData(0, 937.5)]
    [InlineData(-4, 937.5)]
    public void TheCostCapFollowsTheOwnersAccountLevel(int level, double cap) => Assert.Equal(cap, WiredRoomStats.ExecutionCostCapFor(level));

    [Fact]
    public void TheCostCapOverrideIsOnlySetByAPositiveSetting()
    {
        Assert.Null(WiredEngineLimits.FromSettings(_ => "0").ExecutionCostCap);
        Assert.Equal(4687.5, WiredEngineLimits.FromSettings(key => key == "wired.execution_cost_cap" ? "4687.5" : "0").ExecutionCostCap);
        Assert.Null(WiredEngineLimits.FromSettings(key => key == "wired.execution_cost_cap" ? "-1" : "0").ExecutionCostCap);
        Assert.Null(WiredEngineLimits.FromSettings(key => key == "wired.execution_cost_cap" ? "NaN" : "0").ExecutionCostCap);
    }

    [Fact]
    public void ErrorRowsAreTenFixedRowsAndAnUnusedRowReadsZeroAndMinusOne()
    {
        var clock = new ManualClock();
        var log = new WiredErrorLog(clock);
        var rows = Read(Write(new WiredErrorLogsComposer(log.Read()).Compose));

        Assert.Equal(RowNames, rows.Select(row => row.Name));
        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 8], rows.Select(row => row.Id));
        Assert.Equal(Enumerable.Range(0, 10).Select(i => i == 3 ? "WARNING" : "ERROR"), rows.Select(row => row.Category));
        Assert.All(rows, row => Assert.Equal((0, -1L), (row.Count, row.Ms)));
    }

    [Fact]
    public void ARecordedErrorCountsAndAgesAndAClearKeepsAllTenRows()
    {
        var clock = new ManualClock();
        var log = new WiredErrorLog(clock);
        log.Record(WiredErrorKind.PlacementFailure);
        clock.Advance(1500);
        log.Record(WiredErrorKind.PlacementFailure);
        log.Record(WiredErrorKind.ExecutionCap);
        clock.Advance(250);

        var rows = Read(Write(new WiredErrorLogsComposer(log.Read()).Compose));
        Assert.Equal((2, 250L), (rows[8].Count, rows[8].Ms));
        Assert.Equal((0, -1L), (rows[9].Count, rows[9].Ms)); // the other row with id 8 stays apart
        Assert.Equal((1, 250L), (rows[0].Count, rows[0].Ms));

        log.Clear();
        rows = Read(Write(new WiredErrorLogsComposer(log.Read()).Compose));
        Assert.Equal(RowNames, rows.Select(row => row.Name));
        Assert.All(rows, row => Assert.Equal((0, -1L), (row.Count, row.Ms)));
    }

    [Fact]
    public void TheCostDecaysBySixTenthsEveryHalfSecond()
    {
        var cost = new WiredExecutionCost();
        cost.Record(0, 0);
        cost.Record(10, 100);
        var start = cost.Read(10);

        Assert.Equal(start, cost.Read(499), 9); // no step inside one window
        Assert.Equal(start * 0.6, cost.Read(500), 9);
        Assert.Equal(start * 0.36, cost.Read(1000), 9);
        Assert.Equal(start * 0.6 * 0.6 * 0.6, cost.Read(1700), 9);
        Assert.Equal(0d, cost.Read(500 * 100));
    }

    [Fact]
    public void ALoneRepeaterHoldsTheCostNearOne()
    {
        var cost = new WiredExecutionCost();
        cost.Record(0, 0);

        for (var step = 1; step <= 40; step++) {
            cost.Record(step * 500, 1);
        }

        Assert.InRange(cost.Read(40 * 500), 0.99, 1.01);
    }

    private static byte[] Write(Action<IOutgoingPacket> write)
    {
        using var stream = PlusMemoryStream.GetStream();
        write(new FlashOutgoingPacket(stream));

        return stream.ToArray()[6..];
    }

    // Reads the rows as the client does: int, string, string, int, then a signed 64-bit long.
    private static (int Id, string Name, string Category, int Count, long Ms)[] Read(byte[] body)
    {
        var position = 0;
        int Int()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(position));
            position += 4;

            return value;
        }
        string Text()
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(position));
            position += 2;
            var text = Encoding.UTF8.GetString(body, position, length);
            position += length;

            return text;
        }
        long Long()
        {
            var value = BinaryPrimitives.ReadInt64BigEndian(body.AsSpan(position));
            position += 8;

            return value;
        }

        var rows = Enumerable.Range(0, Int()).Select(_ => (Int(), Text(), Text(), Int(), Long())).ToArray();
        Assert.Equal(body.Length, position);

        return rows;
    }

    private sealed class ManualClock : TimeProvider
    {
        private long _now;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => _now;
        public void Advance(long milliseconds) => _now += milliseconds;
    }
}
