using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Preferences;

internal class SetChatDisplayPreferencesEvent(IUserProfileService profiles) : IPacketEvent
{
    private const int FrameLength = sizeof(bool) + 3 * sizeof(int);

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        if (packet.Buffer.Length < FrameLength) {
            return Task.CompletedTask;
        }

        packet.ReadBool();
        var chatMode = packet.ReadInt();
        var bubbleWidth = packet.ReadInt();
        var scrollSpeed = packet.ReadInt();

        return profiles.SetChatDisplayPreferences(session, chatMode, bubbleWidth, scrollSpeed);
    }
}
