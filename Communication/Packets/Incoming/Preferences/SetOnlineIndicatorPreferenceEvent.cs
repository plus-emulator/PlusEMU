using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Preferences;

internal class SetOnlineIndicatorPreferenceEvent(IUserProfileService profiles) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) =>
        packet.Buffer.Length < sizeof(int) ? Task.CompletedTask : profiles.SetOnlineIndicatorPreference(session, packet.ReadInt());
}
