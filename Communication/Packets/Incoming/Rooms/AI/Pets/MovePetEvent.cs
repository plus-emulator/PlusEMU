using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;

namespace Plus.Communication.Packets.Incoming.Rooms.AI.Pets;

// AIR's MovePetMessageComposer: the pet's web id, then the tile and a 0..7 facing.
internal sealed class MovePetEvent(IPetPlacementService pets) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var petId = packet.ReadInt();
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var direction = packet.ReadInt();
        pets.Move(room, session, petId, x, y, direction);

        return Task.CompletedTask;
    }
}
