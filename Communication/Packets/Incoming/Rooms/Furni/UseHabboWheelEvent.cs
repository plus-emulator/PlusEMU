using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

internal sealed class UseHabboWheelEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        // AIR WIN63-202610151745-318700621 package_100/class_3347.as:21 sends only the wheel id.
        furniture.UseHabboWheel(room, session, BuildersClubItemIds.FromWire(packet.ReadUInt()));

        return Task.CompletedTask;
    }
}
