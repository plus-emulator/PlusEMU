using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Settings;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// Habbo's own settings frame: modify mask, read mask, then the timezone,
/// where an empty timezone means the room has no own timezone.
/// </summary>
public sealed class WiredRoomSettingsComposer(WiredRoomSettingsView view) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredRoomSettingsComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(view.ModifyMask);
        packet.WriteInteger(view.InspectMask);
        packet.WriteString(view.TimeZoneId);
    }
}
