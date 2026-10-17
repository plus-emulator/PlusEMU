using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

public sealed class ClickFurniEvent(IFurnitureUseService furniture) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int wireId, category;

        try {
            wireId = packet.ReadInt();
            category = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        // The supported AIR client sends [positive floor id, 0]; a negative id with category 0 stays rejected until its wall/temporary meaning is evidenced.
        var air = category == 0 && wireId > 0;

        if (wireId == 0 || !air && category is not (10 or 20) || packet.HasDataRemaining()) {
            return Task.CompletedTask;
        }

        // WALL uses a negative magnitude; FLOOR uses the id's uint bits, including temporary ids.
        var id = BuildersClubItemIds.FromWire(category == 20 ? (uint)Math.Abs((long)wireId) : unchecked((uint)wireId));
        furniture.Click(room, session, new(id, category == 20));

        return Task.CompletedTask;
    }
}
