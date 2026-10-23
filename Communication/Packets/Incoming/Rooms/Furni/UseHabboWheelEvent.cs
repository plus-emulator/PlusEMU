using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal sealed class UseHabboWheelEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // The client sends only the wheel id.
        furniture.UseHabboWheel(room, session, BuildersClubItemIds.FromWire(packet.ReadUInt()));

        return Task.CompletedTask;
    }
}
