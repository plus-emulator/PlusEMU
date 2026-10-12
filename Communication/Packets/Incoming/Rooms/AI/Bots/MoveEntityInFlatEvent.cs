using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Bots;

// AIR's MoveEntityInFlatMessageComposer: the bot's room index (not its database id), then the tile and a 0..7 facing.
internal sealed class MoveEntityInFlatEvent(IBotManagementService bots) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var virtualId = packet.ReadInt();
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var direction = packet.ReadInt();
        bots.Move(room, session, virtualId, x, y, direction);

        return Task.CompletedTask;
    }
}
