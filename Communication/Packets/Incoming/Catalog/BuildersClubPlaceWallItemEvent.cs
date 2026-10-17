using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Catalog;

/// <summary>
/// [page, offer, extra param, wall location, unused flag] (AIR WIN63-202610151745-318700621, package_16/class_1795.as:11). Sent when a
/// wall furni is dropped from the Builders Club catalogue (HabboCatalog.as:3133) or copied from the info stand with page -1
/// (InfoStandWidget.as:564). The client never sets the flag.
/// </summary>
internal sealed class BuildersClubPlaceWallItemEvent(IBuildersClubService buildersClub) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        var request = new BuildersClubPlacement(packet.ReadInt(), packet.ReadInt(), packet.ReadString());
        var location = packet.ReadString();
        _ = packet.HasDataRemaining() && packet.ReadBool();
        buildersClub.PlaceWallItem(room, session, request, location);

        return Task.CompletedTask;
    }
}
