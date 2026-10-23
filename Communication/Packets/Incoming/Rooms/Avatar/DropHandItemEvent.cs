using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Avatar;

internal class DropHandItemEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        // With a hand item blocker on, the drop is ignored and gets no reply.
        if (user == null || ConfigurationItemStates.HandItemBlocked(room)) {
            return Task.CompletedTask;
        }

        if (user.CarryItemId > 0 && user.CarryTimer > 0) {
            user.CarryItem(0);
        }

        return Task.CompletedTask;
    }
}
