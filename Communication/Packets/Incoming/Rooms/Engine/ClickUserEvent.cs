using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing.Users;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

public sealed class ClickUserEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int virtualId;

        try {
            virtualId = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (packet.HasDataRemaining()) {
            return Task.CompletedTask;
        }

        var actor = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);
        var target = room.GetRoomUserManager().GetRoomUserByVirtualId(virtualId);

        // Clicking a bot fires Click User, with the clicker as the triggering user.
        if (actor == null || actor.IsBot || target == null || target.IsPet || !target.IsBot && target.GetClient()?.GetHabbo() == null) {
            return Task.CompletedTask;
        }

        var result = room.GetWired().DispatchClickUser(actor, target);

        // The server turns the clicker toward the clicked user. The client sends this
        // click instead of a look-at message, so without this turn the clicker would never face them.
        if (result.DoNotRotate) {
            session.Send(new InClientLinkComposer("avatar-info/block-rotate"));
        }
        else {
            actor.TurnToward(target.X, target.Y);
        }

        if (result.BlockMenu) {
            session.Send(new InClientLinkComposer("avatar-info/block-menu"));
        }

        session.Send(new WiredClickUserResponseComposer(virtualId, !result.BlockMenu));

        return Task.CompletedTask;
    }
}
