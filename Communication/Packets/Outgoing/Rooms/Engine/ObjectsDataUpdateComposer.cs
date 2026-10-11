using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>Batch form of <see cref="ObjectDataUpdateComposer"/>: the same legacy data update for several existing floor objects.</summary>
public class ObjectsDataUpdateComposer(IReadOnlyList<(uint ItemId, string Data)> updates) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.ObjectsDataUpdateComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(updates.Count);

        foreach (var (itemId, data) in updates) {
            packet.WriteUInteger(itemId);
            FurnitureDataSerializer.Write(packet, new FurnitureDataSnapshot.Legacy(data), 0, 0);
        }
    }
}
