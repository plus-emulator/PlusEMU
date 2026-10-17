using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Removes several floor objects at once: int count, count × int id, int pickerId, bool byInvisibleFurniManager. AIR
/// WIN63-202610131318-860371703 package_71/class_2976.as:44-60 (incoming 3589). Observed on hhus: Remove Temporary Furni sends
/// [1, 2147401728, -1, false] instead of ObjectRemove.
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
            packet.WriteInteger(unchecked((int)id));
        }

        packet.WriteInteger(_pickerId);
        packet.WriteBoolean(_byInvisibleFurniManager);
    }
}
