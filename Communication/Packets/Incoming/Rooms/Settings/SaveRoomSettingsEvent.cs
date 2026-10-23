using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Settings;

internal sealed class SaveRoomSettingsEvent(IRoomSettingsService settings) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var roomId = packet.ReadUInt();
        var name = packet.ReadString();
        var description = packet.ReadString();
        var access = packet.ReadInt();
        var password = packet.ReadString();
        var maxUsers = packet.ReadInt();
        var category = packet.ReadInt();
        var tagCount = packet.ReadInt();
        var tags = ImmutableArray.CreateBuilder<string>();

        for (var index = 0; index < tagCount; index++) {
            tags.Add(packet.ReadString());
        }

        var tradeSettings = packet.ReadInt();
        var allowPets = packet.ReadBool();
        var allowPetsEat = packet.ReadBool();
        var allowWalkthrough = packet.ReadBool();
        var hidewall = packet.ReadBool();
        var wallThickness = packet.ReadInt();
        var floorThickness = packet.ReadInt();
        var whoMute = packet.ReadInt();
        var whoKick = packet.ReadInt();
        var whoBan = packet.ReadInt();
        var extraFlood = packet.ReadInt();

        // Door-tile leave, idle sleep/autokick and mute-all-pets follow; Plus does not store them yet.
        _ = packet.ReadBool();
        _ = packet.ReadBool();
        _ = packet.ReadInt();
        _ = packet.ReadBool();
        _ = packet.ReadInt();
        _ = packet.ReadBool();

        settings.Save(session, new(roomId, name, description, access, password, maxUsers, category, tags.ToImmutable(),
            tradeSettings, allowPets, allowPetsEat, allowWalkthrough, hidewall, wallThickness, floorThickness,
            whoMute, whoKick, whoBan, extraFlood));

        return Task.CompletedTask;
    }
}
