using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>
/// [type (0 floor, 1 wall), page, offer, extra param, then x, y, direction or the wall location] (AIR WIN63-202610151745-318700621,
/// package_64/class_3565 -> package_71/class_2436.as:40-62). The client asks room.confirm.hide_room and, on OK, sends the placement
/// again with its confirmed flag set (com/sulake/habbo/room/class_2005.as:366-397).
/// </summary>
public sealed class BuildersClubPlacementWarningComposer : IServerPacket
{
    private readonly int _type;
    private readonly int _pageId;
    private readonly int _offerId;
    private readonly string _extraParam;
    private readonly int _x;
    private readonly int _y;
    private readonly int _direction;
    private readonly string _wallLocation;

    private BuildersClubPlacementWarningComposer(int type, int pageId, int offerId, string extraParam, int x, int y, int direction, string wallLocation)
    {
        _type = type;
        _pageId = pageId;
        _offerId = offerId;
        _extraParam = extraParam;
        _x = x;
        _y = y;
        _direction = direction;
        _wallLocation = wallLocation;
    }

    public static BuildersClubPlacementWarningComposer Floor(int pageId, int offerId, string extraParam, int x, int y, int direction) =>
        new(0, pageId, offerId, extraParam, x, y, direction, "");

    public static BuildersClubPlacementWarningComposer Wall(int pageId, int offerId, string extraParam, string wallLocation) =>
        new(1, pageId, offerId, extraParam, 0, 0, 0, wallLocation);

    public uint MessageId => ServerPacketHeader.BuildersClubPlacementWarningComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_type);
        packet.WriteInteger(_pageId);
        packet.WriteInteger(_offerId);
        packet.WriteString(_extraParam);

        if (_type == 0) {
            packet.WriteInteger(_x);
            packet.WriteInteger(_y);
            packet.WriteInteger(_direction);
        }
        else {
            packet.WriteString(_wallLocation);
        }
    }
}
