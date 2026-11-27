using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal sealed class WalkPreviewEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var nonce = packet.ReadInt();
        var roomId = packet.ReadInt();
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var habbo = session.GetHabbo();
        var room = habbo?.CurrentRoom;

        if (!session.SkipsClientExtensions && habbo is { InRoom: true } && room?.RoomId == roomId
            && room.GetRoomUserManager()?.GetRoomUserByHabbo(habbo.Id) is { } actor) {
            room.GetGameMap()?.Navigation?.RequestWalkPreview(actor, nonce, x, y);
        }

        return Task.CompletedTask;
    }
}
