using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.Communication.Packets.Outgoing.Sound;

public class SoundSettingsComposer : IServerPacket
{
    private readonly int[] _volumes;
    private readonly bool _chatPreference;
    private readonly bool _invitesStatus;
    private readonly bool _focusPreference;
    private readonly int _friendBarState;
    private readonly WiredAccountPreferences _wired;

    private const int ChatSize = 0;
    private const int ChatMode = 1;
    private const int ChatBubbleWidth = 1;
    private const int ChatScrollSpeed = 1;
    private const int OnlineIndicator = 0;

    public uint MessageId => ServerPacketHeader.SoundSettingsComposer;

    public SoundSettingsComposer(IEnumerable<int> volumes, bool chatPreference, bool invitesStatus, bool focusPreference, int friendBarState, WiredAccountPreferences wired)
    {
        _volumes = volumes.ToArray();
        _chatPreference = chatPreference;
        _invitesStatus = invitesStatus;
        _focusPreference = focusPreference;
        _friendBarState = friendBarState;
        _wired = wired;
    }

    public void Compose(IOutgoingPacket packet)
    {
        // The client reads three volumes, three bools, two ints,
        // three bools, an int and a bool; everything after that is optional.
        // The five display preferences at the end are not stored yet and keep Habbo's defaults.
        for (var index = 0; index < 3; index++) {
            packet.WriteInteger(index < _volumes.Length ? _volumes[index] : 0);
        }

        packet.WriteBoolean(_chatPreference);
        packet.WriteBoolean(_invitesStatus);
        packet.WriteBoolean(_focusPreference);
        packet.WriteInteger(_friendBarState);
        packet.WriteInteger(0);
        packet.WriteBoolean(_wired.MenuButton);
        packet.WriteBoolean(_wired.InspectButton);
        packet.WriteBoolean(_wired.PlayTestMode);
        packet.WriteInteger(0);
        packet.WriteBoolean(_wired.WhisperDisabled);
        packet.WriteBoolean(_wired.ShowAllNotifications);
        packet.WriteString(_wired.UiStyle);
        packet.WriteInteger(ChatSize);
        packet.WriteInteger(ChatMode);
        packet.WriteInteger(ChatBubbleWidth);
        packet.WriteInteger(ChatScrollSpeed);
        packet.WriteInteger(OnlineIndicator);
    }
}
