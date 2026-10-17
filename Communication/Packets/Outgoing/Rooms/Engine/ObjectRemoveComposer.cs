using System.Globalization;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

public class ObjectRemoveComposer : IServerPacket
{
    private readonly string _itemId;
    private readonly int _userId;

    public uint MessageId => ServerPacketHeader.ObjectRemoveComposer;

    public ObjectRemoveComposer(uint itemId, bool isTemporary, int userId)
    {
        _itemId = isTemporary
            ? unchecked((int)itemId).ToString(CultureInfo.InvariantCulture)
            : BuildersClubItemIds.ToWire(itemId).ToString(CultureInfo.InvariantCulture);
        _userId = userId;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteString(_itemId);
        packet.WriteBoolean(false);
        packet.WriteInteger(_userId);
        packet.WriteInteger(0);
        packet.WriteBoolean(false);
    }
}
