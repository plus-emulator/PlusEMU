using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Preferences;

internal class SetChatStylePreferenceEvent(IUserProfileService profiles) : IPacketEvent
{
    public async Task Parse(GameClient session, IIncomingPacket packet)
    {
        var value = packet.ReadInt();
        var chatSize = packet.Buffer.Length >= sizeof(int) ? packet.ReadInt() : (int?)null;

        await profiles.SetChatStylePreference(session, value);

        if (chatSize is { } size) {
            await profiles.SetChatSize(session, size);
        }
    }
}
