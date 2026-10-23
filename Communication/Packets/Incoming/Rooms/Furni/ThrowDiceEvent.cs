using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal sealed class ThrowDiceEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // The client sends only the dice id.
        furniture.RollDice(room, session, new(BuildersClubItemIds.FromWire(packet.ReadUInt()), 0));

        return Task.CompletedTask;
    }
}
