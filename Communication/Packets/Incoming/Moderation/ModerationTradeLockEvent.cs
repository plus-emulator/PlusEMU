using Plus.Communication.Attributes;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Permissions;

namespace Plus.Communication.Packets.Incoming.Moderation;

[RequiresPermission(PermissionKeys.ModerationTradeLock)]
internal class ModerationTradeLockEvent(ITradeModerationService moderation) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        var message = packet.ReadString();
        var minutes = packet.ReadInt();
        packet.ReadInt(); // topic id; an optional issue id follows (AIR package_84/class_3575.as:14)

        return moderation.Lock(session, userId, minutes, message);
    }
}
