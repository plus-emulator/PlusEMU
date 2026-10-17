using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal class MoveObjectEvent(IRoomItemPlacementService placement) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = BuildersClubItemIds.FromWire(packet.ReadUInt());
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var rotation = packet.ReadInt();
        placement.Move(room, session, itemId, x, y, rotation);

        return Task.CompletedTask;
    }
}
