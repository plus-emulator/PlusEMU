using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.MysteryBoxes;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI.MonsterPlants;

namespace Plus.Communication.Packets.Incoming.Rooms.Engine;

internal sealed class UseFurnitureEvent(IFurnitureUseService furniture, IMonsterPlantSeedService? seeds = null,
    IMysteryBoxService? mysteryBoxes = null) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var itemId = BuildersClubItemIds.FromWire(packet.ReadUInt());
        var parameter = packet.ReadInt();

        if (seeds?.TryPlant(room, session, itemId) == true) {
            return Task.CompletedTask;
        }

        furniture.Use(room, session, new(itemId, parameter));

        return Task.CompletedTask;
    }
}
