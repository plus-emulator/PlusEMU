using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public class ItemRemoveComposer : IServerPacket
{
    private readonly string _itemId;
    private readonly int _userId;
    private readonly bool _byInvisibleFurniManager;

    public uint MessageId => ServerPacketHeader.ItemRemoveComposer;

    public ItemRemoveComposer(uint itemId, int userId, bool byInvisibleFurniManager = false)
    {
        _byInvisibleFurniManager = byInvisibleFurniManager;
        _itemId = BuildersClubItemIds.ToWire(itemId).ToString(CultureInfo.InvariantCulture);
        _userId = userId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_itemId);
        packet.WriteInteger(_userId);
        packet.WriteBoolean(_byInvisibleFurniManager);
    }
}
