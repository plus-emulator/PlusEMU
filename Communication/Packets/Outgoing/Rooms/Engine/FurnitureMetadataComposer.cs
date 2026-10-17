using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Plus-only batch of the furni metadata the AIR entries do not carry: per item id, stackable, isSeat, isBed, walkable,
/// width, length and a teleport target id that is always 0. Sent right after each Objects, ObjectAdd, ObjectUpdate, Items,
/// ItemAdd or ItemUpdate send, to the same recipients.
/// </summary>
public class FurnitureMetadataComposer(IReadOnlyCollection<RoomItemSnapshot> items) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.FurnitureMetadataComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(items.Count);

        foreach (var item in items) {
            var metadata = item.Metadata;

            packet.WriteInteger(unchecked((int)BuildersClubItemIds.ToWire(item.Id)));
            packet.WriteInteger(metadata.Stackable ? 1 : 0);
            packet.WriteInteger(metadata.IsSeat ? 1 : 0);
            packet.WriteInteger(metadata.IsBed ? 1 : 0);
            packet.WriteInteger(metadata.Walkable ? 1 : 0);
            packet.WriteInteger(metadata.Width);
            packet.WriteInteger(metadata.Length);
            packet.WriteInteger(0);
        }
    }
}
