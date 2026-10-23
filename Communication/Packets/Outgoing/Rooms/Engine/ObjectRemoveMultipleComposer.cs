using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Removes several floor objects at once: int count, count × int id, int pickerId, bool byInvisibleFurniManager.
/// Remove Temporary Furni sends [1, 2147401728, -1, false] instead of ObjectRemove.
/// </summary>
public class ObjectRemoveMultipleComposer : IServerPacket
{
    private readonly uint[] _ids;
    private readonly int _pickerId;
    private readonly bool _byInvisibleFurniManager;

    public uint MessageId => ServerPacketHeader.ObjectRemoveMultipleComposer;

    public ObjectRemoveMultipleComposer(IEnumerable<uint> ids, int pickerId = -1, bool byInvisibleFurniManager = false)
    {
        _ids = ids.ToArray();
        _pickerId = pickerId;
        _byInvisibleFurniManager = byInvisibleFurniManager;
    }

    public IReadOnlyList<uint> Ids => _ids;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_ids.Length);

        foreach (var id in _ids) {
            packet.WriteInteger(unchecked((int)BuildersClubItemIds.ToWire(id)));
        }

        packet.WriteInteger(_pickerId);
        packet.WriteBoolean(_byInvisibleFurniManager);
    }
}
