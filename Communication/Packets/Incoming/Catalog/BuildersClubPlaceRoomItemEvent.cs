using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// [page, offer, extra param, x, y, direction, confirmed]. Sent when a furni is dropped from the Builders Club catalogue
/// or copied from the info stand with page -1. The client sets the flag once the user accepted hiding the room.
/// </summary>
internal sealed class BuildersClubPlaceRoomItemEvent(IBuildersClubService buildersClub) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var request = new BuildersClubPlacement(packet.ReadInt(), packet.ReadInt(), packet.ReadString());
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var direction = packet.ReadInt();
        var confirmed = packet.HasDataRemaining() && packet.ReadBool();
        buildersClub.PlaceFloorItem(room, session, request, x, y, direction, confirmed);

        return Task.CompletedTask;
    }
}
