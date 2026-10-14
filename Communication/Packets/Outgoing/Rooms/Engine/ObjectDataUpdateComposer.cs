using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>Updates only the legacy data of an existing floor object; the client keeps its position and height.</summary>
public class ObjectDataUpdateComposer(uint itemId, string data) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ObjectDataUpdateComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(itemId.ToString(CultureInfo.InvariantCulture));
        FurnitureDataSerializer.Write(packet, new FurnitureDataSnapshot.Legacy(data), 0, 0);
    }
}
