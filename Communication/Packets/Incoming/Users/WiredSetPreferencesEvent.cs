using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Incoming.Users;

/// <summary>Habbo's own account preferences save. It gets no reply.</summary>
internal class WiredSetPreferencesEvent(IUserProfileService profiles) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        WiredAccountPreferences preferences;

        try {
            var menuButton = packet.ReadBool();
            var inspectButton = packet.ReadBool();
            var playTestMode = packet.ReadBool();
            packet.ReadInt();
            var whisperDisabled = packet.ReadBool();
            var showAllNotifications = packet.ReadBool();
            preferences = new WiredAccountPreferences(menuButton, inspectButton, playTestMode, whisperDisabled, showAllNotifications, packet.ReadString());
        }
        catch (Exception e) when (e is ArgumentException or IndexOutOfRangeException) {
            return Task.CompletedTask;
        }

        return packet.HasDataRemaining() ? Task.CompletedTask : profiles.SetWiredPreferences(session, preferences);
    }
}
