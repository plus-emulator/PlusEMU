using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Leaderboard;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// The room's published leaderboards: package_160/class_3569 -> package_135/class_2558 reads [n (0..4), descriptor x n], each descriptor
/// per class_2737.fromMessage (L48-83). The client drops overlays the snapshot no longer lists.
/// </summary>
public sealed class WiredVariableLeaderboardSnapshotComposer(IReadOnlyList<WiredLeaderboardDescriptor> descriptors) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredVariableLeaderboardSnapshotComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(descriptors.Count);

        foreach (var descriptor in descriptors) {
            var config = descriptor.Config;
            packet.WriteInteger(descriptor.OverlayInstanceId);
            packet.WriteInteger(descriptor.QueryRevision);
            packet.WriteString(config.Title);
            packet.WriteString(config.Description);
            packet.WriteInteger(config.TileX);
            packet.WriteInteger(config.TileY);
            packet.WriteInteger(config.OffsetX);
            packet.WriteInteger(config.Altitude);
            packet.WriteInteger(config.Sort);
            packet.WriteInteger(config.Columns.Length);

            foreach (var column in config.Columns) {
                packet.WriteInteger(column.Type);
                packet.WriteString(column.Label);
                packet.WriteInteger(column.Width);
            }

            packet.WriteInteger(descriptor.Choices.Count);

            foreach (var choice in descriptor.Choices) {
                packet.WriteString(choice.VariableId);
                packet.WriteString(choice.Name);
            }

            packet.WriteString(descriptor.DefaultVariableId);
            packet.WriteInteger(config.PageSizeOption);
        }
    }
}

/// <summary>
/// Rows of one subscribed chunk: package_160/class_3168 -> package_135/class_3018 reads [overlay, revision, variable, chunk, total,
/// n (0..50), row x n]; each row per class_3133.fromMessage (L47-62), whose two times are readLong (two 32-bit halves).
/// </summary>
public sealed class WiredVariableLeaderboardChunkComposer(WiredLeaderboardChunk chunk) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredVariableLeaderboardChunkComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(chunk.OverlayInstanceId);
        packet.WriteInteger(chunk.QueryRevision);
        packet.WriteString(chunk.VariableId);
        packet.WriteInteger(chunk.ChunkIndex);
        packet.WriteInteger(chunk.TotalCount);
        packet.WriteInteger(chunk.Rows.Count);

        foreach (var row in chunk.Rows) {
            packet.WriteInteger(row.Rank);
            packet.WriteInteger(row.UserId);
            packet.WriteString(row.Username);
            packet.WriteString(row.Figure);
            packet.WriteString(row.RawValue);
            WriteLong(packet, row.CreationTime);
            WriteLong(packet, row.LastUpdateTime);
            packet.WriteBoolean(row.Level != null);

            if (row.Level != null) {
                packet.WriteString(row.Level);
            }

            packet.WriteBoolean(row.TextConnection != null);

            if (row.TextConnection != null) {
                packet.WriteString(row.TextConnection);
            }
        }
    }

    // class_4259.readLong: the high then the low unsigned 32-bit word.
    private static void WriteLong(IOutgoingPacket packet, long value)
    {
        packet.WriteInteger(unchecked((int)(value >> 32)));
        packet.WriteInteger(unchecked((int)value));
    }
}
