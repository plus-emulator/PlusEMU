using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal sealed class ThrowDiceEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // AIR WIN63-202610151745-318700621 package_100/class_3076.as:11 (ROFCAE_DICE_ACTIVATE) sends only the dice id.
        furniture.RollDice(room, session, new(packet.ReadUInt(), 0));

        return Task.CompletedTask;
    }
}
