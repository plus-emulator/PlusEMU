using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Outgoing.Rooms.Settings;

public sealed class RoomSettingsDataComposer(RoomSettingsSnapshot data) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.RoomSettingsDataComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(data.RoomId);
        packet.WriteString(data.Name);
        packet.WriteString(data.Description);
        packet.WriteInteger(data.Access);
        packet.WriteInteger(data.Category);
        packet.WriteInteger(data.UsersMax);
        packet.WriteInteger(data.CapacityLimit);
        packet.WriteInteger(data.Tags.Length);

        foreach (var tag in data.Tags.ToArray()) {
            packet.WriteString(tag);
        }

        packet.WriteInteger(data.TradeSettings); //Trade
        packet.WriteInteger(data.AllowPets ? 1 : 0); // allows pets in room - pet system lacking, so always off
        packet.WriteInteger(data.AllowPetsEating ? 1 : 0); // allows pets to eat your food - pet system lacking, so always off
        packet.WriteInteger(data.RoomBlockingEnabled ? 1 : 0);
        packet.WriteInteger(data.Hidewall ? 1 : 0);
        packet.WriteInteger(data.WallThickness);
        packet.WriteInteger(data.FloorThickness);
        packet.WriteInteger(data.ExtraFlood); // Chat flood sensitivity, the only chat setting the client reads.
        packet.WriteBoolean(false); // Leave on door tile.
        packet.WriteBoolean(false); // Idle sleep.
        packet.WriteInteger(0); // Idle sleep timeout seconds.
        packet.WriteBoolean(false); // Idle autokick.
        packet.WriteInteger(0); // Idle autokick timeout seconds.
        packet.WriteBoolean(false); // Mute all pets.
        packet.WriteInteger(data.WhoCanMute);
        packet.WriteInteger(data.WhoCanKick);
        packet.WriteInteger(data.WhoCanBan);
        // Shows the room-locked notice.
        packet.WriteBoolean(data.HiddenByBuildersClub);
    }
}
