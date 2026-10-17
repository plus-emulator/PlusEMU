using Plus.Communication.Attributes;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.Packets.Incoming.Moderation;

[RequiresPermission(PermissionKeys.ModerationMute)]
internal sealed class ModerationMuteEvent(IModeratorActionService moderation) : IPacketEvent
{
    // AIR sends no length: its only mute action is "Mute 1h" (com/sulake/habbo/moderation/ModActionCtrl.as:65).
    internal const int MuteMinutes = 60;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        packet.ReadString();
        packet.ReadInt(); // topic id; an optional issue id follows (AIR package_84/class_2532.as:14)
        moderation.Mute(session, userId, MuteMinutes);

        return Task.CompletedTask;
    }
}
