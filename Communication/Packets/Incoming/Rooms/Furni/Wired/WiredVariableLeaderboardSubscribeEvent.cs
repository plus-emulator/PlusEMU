using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

/// <summary>
/// [overlay, revision, variable id, n (0..2), distinct chunk index x n]. [0, 0, "0", 0] is the room-open request
/// the client sends on room entry. Other requests subscribe to chunks.
/// </summary>
public sealed class WiredVariableLeaderboardSubscribeEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var overlay = packet.ReadInt();
            var revision = packet.ReadInt();
            var variable = packet.ReadString();
            var count = packet.ReadInt();

            if (count is < 0 or > 2) {
                return Task.CompletedTask;
            }

            var chunks = new int[count];

            for (var index = 0; index < count; index++) {
                chunks[index] = packet.ReadInt();
            }

            if (!packet.HasDataRemaining() && overlay >= 0 && chunks.All(chunk => chunk >= 0) && chunks.Distinct().Count() == count) {
                room.GetWired().SubscribeLeaderboard(session, overlay, revision, variable, chunks);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}
