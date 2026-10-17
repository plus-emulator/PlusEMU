using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>Batch form of <see cref="ObjectDataUpdateComposer"/>: data updates for several existing floor objects.</summary>
public class ObjectsDataUpdateComposer(IReadOnlyList<(uint ItemId, FurnitureDataSnapshot Data)> updates) : IServerPacket
{
    public ObjectsDataUpdateComposer(IReadOnlyList<(uint ItemId, string Data)> updates)
        : this([.. updates.Select(update => (update.ItemId, (FurnitureDataSnapshot)new FurnitureDataSnapshot.Legacy(update.Data)))]) { }

    public uint MessageId => ServerPacketHeader.ObjectsDataUpdateComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(updates.Count);

        foreach (var (itemId, data) in updates) {
            packet.WriteUInteger(itemId);
            FurnitureDataSerializer.Write(packet, data, 0, 0);
        }
    }
}
