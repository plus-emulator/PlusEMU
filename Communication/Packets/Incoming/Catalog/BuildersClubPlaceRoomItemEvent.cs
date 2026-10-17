using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// [page, offer, extra param, x, y, direction, unused flag] (AIR WIN63-202610151745-318700621, package_16/class_2016.as:11). Sent when a
/// furni is dropped from the Builders Club catalogue (HabboCatalog.as:3117-3130) or copied from the info stand with page -1
/// (InfoStandWidget.as:557). The client never sets the flag.
/// </summary>
internal sealed class BuildersClubPlaceRoomItemEvent(IBuildersClubService buildersClub) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var request = new BuildersClubPlacement(packet.ReadInt(), packet.ReadInt(), packet.ReadString());
        var x = packet.ReadInt();
        var y = packet.ReadInt();
        var direction = packet.ReadInt();
        _ = packet.HasDataRemaining() && packet.ReadBool();
        buildersClub.PlaceFloorItem(room, session, request, x, y, direction);

        return Task.CompletedTask;
    }
}
