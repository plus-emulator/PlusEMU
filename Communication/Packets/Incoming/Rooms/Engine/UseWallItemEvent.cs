using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal sealed class UseWallItemEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        furniture.UseWall(room, session, new(TemporaryItemIds.FromWire(BuildersClubItemIds.FromWire(packet.ReadUInt()), true), packet.ReadInt()));

        return Task.CompletedTask;
    }
}
