using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>Updates only the data of an existing floor object; the client keeps its position and height.</summary>
public class ObjectDataUpdateComposer(uint itemId, FurnitureDataSnapshot data) : IServerPacket
{
    public ObjectDataUpdateComposer(uint itemId, string data) : this(itemId, new FurnitureDataSnapshot.Legacy(data)) { }

    public uint MessageId => ServerPacketHeader.ObjectDataUpdateComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(BuildersClubItemIds.ToWire(itemId).ToString(CultureInfo.InvariantCulture));
        FurnitureDataSerializer.Write(packet, data, 0, 0);
    }
}
