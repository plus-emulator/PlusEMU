using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

/// <summary>
/// Plus-only batch of the furni metadata the AIR entries do not carry: per item id, stackable, isSeat, isBed, walkable,
/// width, length and a teleport target id that is always 0. Sent right after each Objects, ObjectAdd, ObjectUpdate, Items,
/// ItemAdd or ItemUpdate send, to the same recipients.
/// </summary>
public class FurnitureMetadataComposer(IReadOnlyCollection<RoomItemSnapshot> items) : IServerPacket, IClientExtensionPacket
{
    // This floor metadata extension has no category field; a same-number wall copy must not overwrite the floor entry.
    private readonly RoomItemSnapshot[] _items = items.Where(item => !TemporaryItemIds.IsWallIdentity(item.Id)).ToArray();

    public uint MessageId => ServerPacketHeader.FurnitureMetadataComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_items.Length);

        foreach (var item in _items) {
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
