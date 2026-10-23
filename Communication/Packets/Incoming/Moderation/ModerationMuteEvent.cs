using Plus.Communication.Attributes;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;

namespace Plus.Communication.Packets.Incoming.Moderation;

[RequiresPermission(PermissionKeys.ModerationMute)]
internal sealed class ModerationMuteEvent(IModeratorActionService moderation) : IPacketEvent
{
    // The client sends no length. Its only mute action is "Mute 1h".
    internal const int MuteMinutes = 60;

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var userId = packet.ReadInt();
        packet.ReadString();
        packet.ReadInt(); // topic id; an optional issue id follows
        moderation.Mute(session, userId, MuteMinutes);

        return Task.CompletedTask;
    }
}
