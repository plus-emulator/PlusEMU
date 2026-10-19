using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public sealed class RoomEventComposer(RoomEventSnapshot data) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RoomEventComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // package_52/class_3609: ad id, owner id, owner name, flat id, event type, name, description, minutes since creation, minutes
        // left, category. Observed on AIR WIN63-202610151745-318700621: a room without an event sends -1 for the ids, type and category.
        packet.WriteInteger(data.RoomId);
        packet.WriteInteger(data.OwnerId);
        packet.WriteString(data.OwnerName);
        packet.WriteInteger(data.Active ? 1 : -1);
        packet.WriteInteger(data.Active ? 0 : -1);
        packet.WriteString(data.Name);
        packet.WriteString(data.Description);
        packet.WriteInteger(0);
        packet.WriteInteger(0);
        packet.WriteInteger(data.Active ? 0 : -1);
    }
}
