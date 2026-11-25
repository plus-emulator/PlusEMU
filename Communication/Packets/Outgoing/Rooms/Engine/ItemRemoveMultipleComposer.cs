using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>Removes several wall items at once: int count, count × int id, int pickerId, bool byInvisibleFurniManager.</summary>
public class ItemRemoveMultipleComposer(IEnumerable<uint> ids, int pickerId = -1, bool byInvisibleFurniManager = false) : IServerPacket
{
    private readonly uint[] _ids = ids.ToArray();

    public uint MessageId => ServerPacketHeader.ItemRemoveMultipleComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_ids.Length);

        foreach (var id in _ids) {
            packet.WriteInteger(unchecked((int)BuildersClubItemIds.ToWire(id)));
        }

        packet.WriteInteger(pickerId);
        packet.WriteBoolean(byInvisibleFurniManager);
    }
}
