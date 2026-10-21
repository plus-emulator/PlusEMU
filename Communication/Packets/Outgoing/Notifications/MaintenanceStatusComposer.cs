using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Notifications;

public class MaintenanceStatusComposer(bool isInMaintenance, int minutesUntilMaintenance, int durationMinutes) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.MaintenanceStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(isInMaintenance);
        packet.WriteInteger(minutesUntilMaintenance);
        packet.WriteInteger(durationMinutes);
    }
}
