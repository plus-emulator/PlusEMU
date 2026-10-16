using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms.Instance;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// The monitor tab. Usage is every execution in the last window against the per-pass budget,
/// times are whole milliseconds per pass and recursion is the deepest chain. Plus has no heavy,
/// overload or kill state, so heavy is false, killed is 0 and their six thresholds are 0.
/// </summary>
public sealed class WiredMonitorDataComposer(WiredMonitorSnapshot snapshot) : IServerPacket
{
    private readonly WiredMonitorSnapshot _captured = snapshot with { Logs = snapshot.Logs with { Tallies = snapshot.Logs.Tallies.ToImmutableArray(), Recent = snapshot.Logs.Recent.ToImmutableArray() } };
    public uint MessageId => ServerPacketHeader.WiredMonitorDataComposer;
    public void Compose(IOutgoingPacket packet)
    {
        var engine = _captured.Engine;
        packet.WriteInteger(engine.Executions);
        packet.WriteInteger(_captured.ExecutionsPerPass);
        packet.WriteBoolean(false);
        packet.WriteInteger(engine.Pending);
        packet.WriteInteger(_captured.PendingLimit);
        packet.WriteInteger(engine.AverageMs);
        packet.WriteInteger(engine.PeakMs);
        packet.WriteInteger(engine.PeakDepth);
        packet.WriteInteger(_captured.DepthLimit);
        packet.WriteInteger(0);
        packet.WriteInteger(engine.WindowMs);

        for (var threshold = 0; threshold < 6; threshold++) {
            packet.WriteInteger(0);
        }

        packet.WriteInteger(_captured.Logs.Tallies.Count);

        foreach (var tally in _captured.Logs.Tallies) {
            var latest = tally.Latest;
            packet.WriteString(WiredRoomLogEntry.TypeName(tally.Source));
            packet.WriteString(WiredRoomLogEntry.LevelName(latest?.Level ?? (tally.Source == WiredLogSource.WiredLog ? WiredRoomLog.InfoLevel : WiredRoomLog.ErrorLevel)));
            packet.WriteInteger(tally.Count);
            packet.WriteInteger(latest == null ? 0 : Seconds(latest));
            packet.WriteString(latest?.Reason ?? "");
            packet.WriteString(latest?.Label ?? "");
            packet.WriteInteger(unchecked((int)(latest?.BoxId ?? 0)));
        }

        packet.WriteInteger(_captured.Logs.Recent.Count);

        foreach (var entry in _captured.Logs.Recent) {
            packet.WriteString(WiredRoomLogEntry.TypeName(entry.Source));
            packet.WriteString(WiredRoomLogEntry.LevelName(entry.Level));
            packet.WriteInteger(Seconds(entry));
            packet.WriteString(entry.Reason);
            packet.WriteString(entry.Label);
            packet.WriteInteger(unchecked((int)entry.BoxId));
        }
    }
    private static int Seconds(WiredRoomLogEntry entry) => (int)Math.Clamp(entry.Timestamp.ToUnixTimeSeconds(), 0, int.MaxValue);
}

/// <summary>Official AIR <c>WiredLogPage</c>: a 1-based page of the room log and the filters that produced it.</summary>
public sealed class WiredRoomLogPageComposer(WiredRoomLogPage page, int levelFilter, int sourceFilter, string query) : IServerPacket
{
    private readonly WiredRoomLogPage _captured = page with { Entries = page.Entries.ToImmutableArray() };
    public uint MessageId => ServerPacketHeader.WiredRoomLogPageComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_captured.Total);
        packet.WriteInteger(_captured.Page + 1);
        packet.WriteInteger(_captured.Amount);
        packet.WriteInteger(_captured.Entries.Count);

        foreach (var entry in _captured.Entries) {
            packet.WriteInteger(unchecked((int)(entry.Id >> 32)));
            packet.WriteInteger(unchecked((int)entry.Id));
            packet.WriteByte((byte)entry.Level);
            packet.WriteByte((byte)entry.WireSource);
            packet.WriteString(entry.Message);
            WiredVariableHoldersPageComposer.WriteTimestamp(packet, entry.Timestamp);
        }

        packet.WriteBoolean(levelFilter >= 0);

        if (levelFilter >= 0) {
            packet.WriteByte((byte)levelFilter);
        }

        packet.WriteBoolean(sourceFilter >= 0);

        if (sourceFilter >= 0) {
            packet.WriteByte((byte)sourceFilter);
        }

        packet.WriteBoolean(query.Length > 0);

        if (query.Length > 0) {
            packet.WriteString(query);
        }
    }
}

/// <summary>
/// Official AIR monitor statistics, read by <c>package_121/WiredRoomStatsData.as:34-50</c> (WIN63-202610131318-860371703):
/// cost and cost cap as doubles, heavy, then floor, wall and the three permanent variable kinds as count / cap pairs.
/// </summary>
public sealed class WiredRoomStatsComposer(WiredRoomStats stats) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredRoomStatsComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteDouble(stats.ExecutionCost);
        packet.WriteDouble(stats.ExecutionCostCap);
        packet.WriteBoolean(stats.IsHeavy);
        packet.WriteInteger(stats.FloorItems);
        packet.WriteInteger(stats.FloorItemCap);
        packet.WriteInteger(stats.WallItems);
        packet.WriteInteger(stats.WallItemCap);
        packet.WriteInteger(stats.PermanentFurniVariables);
        packet.WriteInteger(stats.MaxPermanentFurniVariables);
        packet.WriteInteger(stats.PermanentUserVariables);
        packet.WriteInteger(stats.MaxPermanentUserVariables);
        packet.WriteInteger(stats.PermanentGlobalVariables);
        packet.WriteInteger(stats.MaxPermanentGlobalVariables);
    }
}

/// <summary>
/// Official AIR monitor error rows: a count, then per row (<c>package_121/class_4361.as:19-28</c>, WIN63-202610131318-860371703)
/// error id, name, category, throw count and the milliseconds since the last occurrence as a long (-1 for never).
/// </summary>
public sealed class WiredErrorLogsComposer(IReadOnlyList<WiredErrorRow> rows) : IServerPacket
{
    private readonly ImmutableArray<WiredErrorRow> _rows = rows.ToImmutableArray();
    public uint MessageId => ServerPacketHeader.WiredErrorLogsComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_rows.Length);

        foreach (var row in _rows) {
            packet.WriteInteger(row.ErrorId);
            packet.WriteString(row.ErrorName);
            packet.WriteString(row.Category);
            packet.WriteInteger(row.ThrowCount);
            packet.WriteInteger(unchecked((int)(row.MsSinceLastOccurrence >> 32)));
            packet.WriteInteger(unchecked((int)row.MsSinceLastOccurrence));
        }
    }
}
