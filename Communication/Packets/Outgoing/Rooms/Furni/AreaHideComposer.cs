using Plus.HabboHotel.Items.AreaHide;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

// Seven fields. The invisibility and wall item options are not in the message.
public sealed class AreaHideComposer(uint itemId, AreaHideValues state, bool removed = false) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.AreaHideComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(BuildersClubItemIds.ToWire(itemId));
        packet.WriteBool(!removed && state.On);
        packet.WriteInt(state[1]);
        packet.WriteInt(state[2]);
        packet.WriteInt(state[3]);
        packet.WriteInt(state[4]);
        packet.WriteBool(state[7] == 1);
    }
}
