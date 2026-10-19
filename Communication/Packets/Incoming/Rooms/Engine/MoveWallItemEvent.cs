using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class MoveWallItemEvent(IRoomItemPlacementService placement) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = TemporaryItemIds.FromWire(BuildersClubItemIds.FromWire(packet.ReadUInt()), true);
        var location = packet.ReadString();
        placement.MoveWall(room, session, itemId, location);

        return Task.CompletedTask;
    }
}
